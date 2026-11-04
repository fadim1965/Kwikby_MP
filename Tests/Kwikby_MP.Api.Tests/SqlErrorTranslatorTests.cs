using kwikby_MP.Api.Infrastructure;
using Xunit;

namespace Kwikby_MP.Api.Tests;

public sealed class SqlErrorTranslatorTests
{
    [Theory]
    [InlineData(52001, 400)]
    [InlineData(52002, 400)]
    [InlineData(52003, 404)]
    [InlineData(52004, 409)]
    [InlineData(52005, 409)]
    [InlineData(52006, 409)]
    [InlineData(52007, 403)]
    [InlineData(52008, 409)]
    [InlineData(2627, 409)]
    [InlineData(2601, 409)]
    [InlineData(547, 409)]
    [InlineData(1205, 503)]
    [InlineData(-2, 504)]
    [InlineData(4060, 503)]
    [InlineData(18456, 503)]
    [InlineData(99999, 500)]
    public void Sql_numbers_map_to_http_status(int number, int expectedStatus)
    {
        var exception = SqlErrorTranslator.Translate(number, "message");
        Assert.Equal(expectedStatus, exception.StatusCode);
    }

    [Fact]
    public void Custom_procedure_message_is_preserved()
    {
        var exception = SqlErrorTranslator.Translate(52003, "Product variant not found.");
        Assert.Equal("Product variant not found.", exception.Message);
        Assert.Equal("not_found", exception.Code);
    }

    [Fact]
    public void Unknown_number_does_not_leak_the_raw_message()
    {
        var exception = SqlErrorTranslator.Translate(99999, "Server=secret;Password=secret");
        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
