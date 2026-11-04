using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Kwikby_MP.Api.Tests;

/// <summary>
/// Contract tests that require a deployed SQL Server. They are part of the normal
/// test run but return early when no test connection string is present, so the
/// suite still passes on a machine without SQL Server (like this one).
///
/// To run them:
///   $env:API_TEST_SQL = "Data Source=.;Initial Catalog=MVStores_ApiTest;..."
///   dotnet test Tests\\Kwikby_MP.Api.Tests\\Kwikby_MP.Api.Tests.csproj
/// </summary>
public sealed class SqlContractTests
{
    private static readonly string[] ApiProcedures =
    {
        "usp_SearchProducts",
        "usp_GetProductDetail",
        "usp_GetShopDashboard",
        "usp_GetPlatformDashboard",
        "usp_AdjustStock",
        "usp_Auth_GetUserForLogin",
        "usp_Auth_GetUserClaims",
        "usp_Auth_RecordLogin",
        "usp_Auth_RecordFailedLogin",
        "usp_Category_List",
        "usp_Shop_List",
        "usp_Shop_Get"
    };

    private static string? ConnectionString =>
        Environment.GetEnvironmentVariable("API_TEST_SQL")
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__MultiVendorsStoresConn");

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Every_procedure_the_api_calls_exists()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return; // no SQL Server configured for this run
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var names = (await connection.QueryAsync<string>(
                "SELECT name FROM sys.procedures WHERE name LIKE 'usp[_]%'"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var procedure in ApiProcedures)
        {
            Assert.Contains(procedure, names);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Auth_procedure_parameters_match_the_api_calls()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        var rows = await connection.QueryAsync<(string Procedure, string Parameter)>(
            "SELECT OBJECT_NAME(p.object_id) AS [Procedure], p.name AS [Parameter] " +
            "FROM sys.parameters p " +
            "WHERE OBJECT_NAME(p.object_id) LIKE 'usp[_]Auth[_]%'");

        var parameters = rows
            .GroupBy(r => r.Procedure, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.Parameter.TrimStart('@')).ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Email", parameters["usp_Auth_GetUserForLogin"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("UserId", parameters["usp_Auth_GetUserClaims"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("UserId", parameters["usp_Auth_RecordLogin"], StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Email", parameters["usp_Auth_RecordFailedLogin"], StringComparer.OrdinalIgnoreCase);
    }
}
