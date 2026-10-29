using WebDS_Test.Data;
using WebDS_Test.Services;
using Xunit;

namespace WebDS_Test.Tests;

/// <summary>
/// Pins the start-up guards that keep a production host off the demo defaults.
/// </summary>
public class ProductionGuardsTests
{
    [Fact]
    public void Development_keeps_the_sqlite_fallback_for_a_fresh_clone()
    {
        ProductionGuards.RequireSqlServerWhenRequested(true, true, "", false);
    }

    [Fact]
    public void Production_refuses_a_sql_server_request_with_no_connection_string()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductionGuards.RequireSqlServerWhenRequested(false, true, " ", false));
    }

    [Fact]
    public void Production_allows_sqlite_when_it_was_not_sql_server_that_was_asked_for()
    {
        ProductionGuards.RequireSqlServerWhenRequested(false, false, null, false);
    }

    [Fact]
    public void Production_allows_an_explicit_sqlite_fallback()
    {
        ProductionGuards.RequireSqlServerWhenRequested(false, true, null, true);
    }

    [Fact]
    public void Production_accepts_a_configured_connection_string()
    {
        ProductionGuards.RequireSqlServerWhenRequested(false, true, "Data Source=.;Initial Catalog=MVStores;", false);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dev-simulator-webhook-secret")]
    public void Production_rejects_a_missing_or_default_webhook_secret(string? secret)
    {
        Assert.Throws<InvalidOperationException>(() => ProductionGuards.RequireRealWebhookSecret(false, secret));
    }

    [Fact]
    public void Production_accepts_a_private_webhook_secret()
    {
        ProductionGuards.RequireRealWebhookSecret(false, "a-long-private-value");
    }

    [Fact]
    public void Development_accepts_the_default_webhook_secret()
    {
        ProductionGuards.RequireRealWebhookSecret(true, null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(DbSeeder.DefaultAdminPassword)]
    public void Production_rejects_the_demo_admin_password_on_an_empty_store(string? password)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductionGuards.RequireNonDefaultSeedCredentials(false, false, password));
    }

    [Fact]
    public void An_existing_store_is_not_blocked_by_the_seed_password_check()
    {
        ProductionGuards.RequireNonDefaultSeedCredentials(false, true, DbSeeder.DefaultAdminPassword);
    }

    [Fact]
    public void Production_accepts_a_private_admin_password_on_an_empty_store()
    {
        ProductionGuards.RequireNonDefaultSeedCredentials(false, false, "S0me-private-Passw0rd!");
    }
}
