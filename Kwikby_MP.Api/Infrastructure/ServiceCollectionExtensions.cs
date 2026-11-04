using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using kwikby_MP.Api.Repositories;
using kwikby_MP.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace kwikby_MP.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddApplicationServices(configuration, environment);

        // Read + validate once for registration-time decisions (JWT key, CORS, limits).
        var apiOptions = configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();
        apiOptions.Validate(environment);

        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.SectionName))
            .PostConfigure(options => options.Validate(environment));

        // ------------------------------------------------------------------
        // Data access: Dapper + Microsoft.Data.SqlClient only. No EF Core.
        // ------------------------------------------------------------------
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IStoredProcedureGateway, StoredProcedureGateway>();
        services.AddSingleton<JwtTokenService>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IProductApiRepository, ProductApiRepository>();
        services.AddScoped<ICategoryApiRepository, CategoryApiRepository>();
        services.AddScoped<IShopApiRepository, ShopApiRepository>();
        services.AddScoped<IDashboardApiRepository, DashboardApiRepository>();
services.AddScoped<IInventoryApiRepository, InventoryApiRepository>();
services.AddScoped<IAuditApiRepository, AuditApiRepository>();

        // ------------------------------------------------------------------
        // MVC / JSON / errors
        // ------------------------------------------------------------------
        services.AddProblemDetails();
        services.AddScoped<ApiExceptionFilter>();

        services.AddControllers(options =>
            {
                options.Filters.Add<ApiExceptionFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // Automatic [ApiController] model validation also answers with
        // application/problem+json, matching the exception filter and the auth handlers.
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problem = new ValidationProblemDetails(context.ModelState)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed",
                    Detail = "One or more fields are invalid.",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                return new ObjectResult(problem)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        services.AddEndpointsApiExplorer();

        // ------------------------------------------------------------------
        // Authentication: JWT bearer only. The MVC cookie scheme is not used here.
        // ------------------------------------------------------------------
        var jwt = apiOptions.Jwt;
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names exactly as issued (sub, email, role, shop_id) instead
                // of letting the handler rewrite them to the legacy WS-Fed URIs.
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = "email",
                    RoleClaimType = "role"
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Status = StatusCodes.Status401Unauthorized,
                            Title = "Unauthorized",
                            Detail = "A valid bearer token is required.",
                            Instance = context.Request.Path,
                            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
                        }, options: null, contentType: "application/problem+json");
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Status = StatusCodes.Status403Forbidden,
                            Title = "Forbidden",
                            Detail = "This token does not have access to the requested resource.",
                            Instance = context.Request.Path,
                            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
                        }, options: null, contentType: "application/problem+json");
                    }
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ApiPolicies.Authenticated, policy => policy
                .RequireAuthenticatedUser()
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));

            options.AddPolicy(ApiPolicies.Admin, policy => policy
                .RequireRole("Admin")
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));

            // Vendor access is a shop membership, not a role. Admins pass as well.
            options.AddPolicy(ApiPolicies.Vendor, policy => policy
                .RequireAssertion(context =>
                    context.User.IsInRole("Admin") ||
                    context.User.FindAll("shop_id").Any())
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));

            options.AddPolicy(ApiPolicies.Customer, policy => policy
                .RequireAssertion(context =>
                    context.User.IsInRole("Admin") || context.User.IsInRole("Customer"))
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme));
        });

        // ------------------------------------------------------------------
        // CORS: explicit origins only. Empty list = no cross-origin browser access.
        // ------------------------------------------------------------------
        services.AddCors(options =>
        {
            options.AddPolicy(ApiCors.Policy, policy =>
            {
                if (apiOptions.Cors.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(apiOptions.Cors.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                }
                else
                {
                    policy.SetIsOriginAllowed(_ => false);
                }
            });
        });

        // ------------------------------------------------------------------
        // Rate limiting: one partition per token (or IP for anonymous callers).
        // ------------------------------------------------------------------
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ApiRateLimitPolicies.Api, httpContext =>
            {
                var subject = httpContext.User.Identity?.IsAuthenticated == true
                    ? httpContext.User.FindFirst("sub")?.Value ?? "token"
                    : httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"api:{subject}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, apiOptions.RateLimit.PermitLimit),
                        Window = TimeSpan.FromSeconds(Math.Max(1, apiOptions.RateLimit.WindowSeconds)),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Wait a moment and retry the request.",
                    Instance = context.HttpContext.Request.Path,
                    Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
                }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            };
        });

        // ------------------------------------------------------------------
        // Swagger / OpenAPI with a bearer-token security definition.
        // ------------------------------------------------------------------
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Kwikby API",
                Version = "v1",
                Description = "LibanPost eShop marketplace API. All data access goes through SQL Server stored procedures."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the accessToken value from POST /api/v1/auth/token."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                }] = Array.Empty<string>()
            });
        });

        // ------------------------------------------------------------------
        // Health checks
        // ------------------------------------------------------------------
        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: new[] { "ready" });

        return services;
    }
}
