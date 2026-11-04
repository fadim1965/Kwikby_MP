namespace kwikby_MP.Api.Infrastructure;

/// <summary>
/// Bound from the <c>Api</c> configuration section. Everything the API needs that is
/// not already supplied by the host (SQL connection, JWT, CORS, rate limiting).
/// </summary>
public sealed class ApiOptions
{
    public const string SectionName = "Api";

    /// <summary>Development fallback only. Production refuses to start without a real key.</summary>
    public const string DevelopmentSigningKey = "dev-only-kwikby-api-signing-key-change-me";

    public bool Enabled { get; set; } = true;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int SlowCommandMs { get; set; } = 1_000;

    public JwtOptions Jwt { get; set; } = new();

    public CorsOptions Cors { get; set; } = new();

    public ApiRateLimitOptions RateLimit { get; set; } = new();

    /// <summary>Fail fast on unsafe production configuration; fill in safe development defaults.</summary>
    public void Validate(IHostEnvironment environment)
    {
        if (CommandTimeoutSeconds <= 0) CommandTimeoutSeconds = 30;
        if (SlowCommandMs <= 0) SlowCommandMs = 1_000;
        if (Jwt.AccessTokenMinutes <= 0) Jwt.AccessTokenMinutes = 60;

        if (string.IsNullOrWhiteSpace(Jwt.SigningKey) || Jwt.SigningKey.Length < 32)
        {
            if (environment.IsDevelopment())
            {
                // Keeps a fresh clone runnable. The key is public and must never be used
                // outside Development; appsettings.Development.json usually supplies it.
                Jwt.SigningKey = DevelopmentSigningKey;
            }
            else
            {
                throw new InvalidOperationException(
                    "Api:Jwt:SigningKey must be configured with at least 32 characters outside Development. " +
                    "Set it in appsettings.Local.json or the Api__Jwt__SigningKey environment variable.");
            }
        }
    }
}

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "kwikby_MP";

    public string Audience { get; set; } = "kwikby_MP.Api";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;
}

public sealed class CorsOptions
{
    /// <summary>Explicit origins. Empty means no cross-origin browser access (same-origin only).</summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}

public sealed class ApiRateLimitOptions
{
    public int PermitLimit { get; set; } = 100;

    public int WindowSeconds { get; set; } = 60;
}
