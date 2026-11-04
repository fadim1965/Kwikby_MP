using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace kwikby_MP.Api.Infrastructure;

public static class PipelineExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // Any exception that escapes MVC (middleware, routing, DI) still answers with
        // ProblemDetails, never HTML.
        app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
        {
            var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            var exception = feature?.Error;
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("ApiExceptionHandler");

            if (exception is ApiException apiException)
            {
                context.Response.StatusCode = apiException.StatusCode;
                await context.Response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = apiException.StatusCode,
                    Title = apiException.Title,
                    Detail = apiException.Message,
                    Instance = context.Request.Path
                }, options: null, contentType: "application/problem+json");
                return;
            }

            logger.LogError(exception, "Unhandled exception outside MVC for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error",
                Detail = "An unexpected error occurred. Contact support with the trace id.",
                Instance = context.Request.Path,
                Extensions = { ["traceId"] = context.TraceIdentifier }
            }, options: null, contentType: "application/problem+json");
        }));

        app.UseHttpsRedirection();

        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next();
        });

        // Operational kill switch. Health probes and Swagger stay available so an
        // operator can still see the process state while the API is drained.
        var apiOptions = app.Services.GetRequiredService<IOptions<ApiOptions>>().Value;
        if (!apiOptions.Enabled)
        {
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsJsonAsync(new ProblemDetails
                    {
                        Status = StatusCodes.Status503ServiceUnavailable,
                        Title = "API disabled",
                        Detail = "The API is temporarily disabled for maintenance.",
                        Instance = context.Request.Path,
                        Extensions = { ["traceId"] = context.TraceIdentifier }
                    }, options: null, contentType: "application/problem+json");
                    return;
                }

                await next();
            });
        }

        app.UseRouting();
        app.UseCors(ApiCors.Policy);
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Kwikby API v1");
                options.RoutePrefix = "swagger";
            });
        }

        app.MapControllers();

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealth
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealth
        });

        return app;
    }

    private static Task WriteHealth(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        });
    }
}
