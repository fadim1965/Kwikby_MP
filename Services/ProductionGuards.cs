using kwikby_MP.Data;

namespace kwikby_MP.Services;

/// <summary>
/// Start-up checks that stop a non-Development deployment from running on the demo defaults.
/// </summary>
/// <remarks>
/// Each guard throws <see cref="InvalidOperationException"/> with a message that names the setting
/// to change, so a misconfigured production host fails at boot instead of quietly serving an empty
/// SQLite store, a public webhook secret or the published demo administrator password. They take
/// plain values rather than the host environment so they can be unit-tested without a web host.
/// </remarks>
public static class ProductionGuards
{
    /// <summary>
    /// SQL Server was asked for but no connection string was supplied. In Development this keeps
    /// the documented fresh-clone behaviour (fall back to SQLite); anywhere else it is an error
    /// unless <c>Database:AllowSqliteFallback</c> is set deliberately.
    /// </summary>
    public static void RequireSqlServerWhenRequested(
        bool isDevelopment, bool sqlServerRequested, string? sqlServerConnectionString, bool allowSqliteFallback)
    {
        if (isDevelopment || allowSqliteFallback || !sqlServerRequested)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sqlServerConnectionString))
        {
            throw new InvalidOperationException(
                "Database:Provider is 'SqlServer' but ConnectionStrings:MultiVendorsStoresConn is empty. " +
                "Refusing to fall back to a new SQLite store outside Development. Supply the connection " +
                "string (appsettings.Local.json or ConnectionStrings__MultiVendorsStoresConn), or set " +
                "Database:AllowSqliteFallback=true if SQLite is intended.");
        }
    }

    /// <summary>
    /// The simulator signs webhooks with a shared secret. Its built-in default is public, so outside
    /// Development the secret must be supplied and must differ from that default.
    /// </summary>
    public static void RequireRealWebhookSecret(bool isDevelopment, string? configuredSecret)
    {
        if (isDevelopment)
        {
            return;
        }

        var builtInDefault = new PaymentSettings().WebhookSecret;
        if (string.IsNullOrWhiteSpace(configuredSecret) ||
            string.Equals(configuredSecret, builtInDefault, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Payments:Provider is 'simulator' but Payments:WebhookSecret is missing or is the public " +
                "development default; anyone could forge a 'paid' webhook. Set a private secret " +
                "(appsettings.Local.json or Payments__WebhookSecret), or use Payments:Provider='sandbox'.");
        }
    }

    /// <summary>
    /// The first start creates an administrator. Outside Development that account must not use the
    /// password published in the repository. Only checked while the store has no users yet, so an
    /// existing deployment is not blocked after the account has been created.
    /// </summary>
    public static void RequireNonDefaultSeedCredentials(
        bool isDevelopment, bool storeHasUsers, string? configuredAdminPassword)
    {
        if (isDevelopment || storeHasUsers)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuredAdminPassword) ||
            string.Equals(configuredAdminPassword, DbSeeder.DefaultAdminPassword, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The store is empty and Seed:AdminPassword is missing or is the published demo password. " +
                "Set Seed:AdminPassword (and Seed:AdminEmail) in appsettings.Local.json or the environment " +
                "before the first start outside Development.");
        }
    }
}
