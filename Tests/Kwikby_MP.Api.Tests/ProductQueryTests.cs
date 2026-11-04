using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using Xunit;

namespace Kwikby_MP.Api.Tests;

public sealed class ProductQueryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(4, 4)]
    public void Page_is_clamped_to_at_least_one(int input, int expected)
    {
        var query = new ProductQuery { Page = input };
        query.Normalize();
        Assert.Equal(expected, query.Page);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(500, 100)]
    [InlineData(50, 50)]
    public void PageSize_is_clamped_between_one_and_one_hundred(int input, int expected)
    {
        var query = new ProductQuery { PageSize = input };
        query.Normalize();
        Assert.Equal(expected, query.PageSize);
    }

    [Theory]
    [InlineData("relevance")]
    [InlineData("price_asc")]
    [InlineData("price_desc")]
    [InlineData("rating")]
    [InlineData("newest")]
    [InlineData("PRICE_ASC")]
    public void Allowed_sorts_are_accepted(string sortBy)
    {
        var query = new ProductQuery { SortBy = sortBy };
        query.Normalize();
        Assert.Equal(sortBy.ToLowerInvariant(), query.SortBy);
    }

    [Fact]
    public void Unknown_sort_is_rejected_with_400()
    {
        var query = new ProductQuery { SortBy = "name; DROP TABLE Products" };
        var exception = Assert.Throws<ApiException>(() => query.Normalize());
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("validation", exception.Code);
    }

    [Fact]
    public void Min_price_greater_than_max_price_is_rejected()
    {
        var query = new ProductQuery { MinPrice = 10m, MaxPrice = 5m };
        var exception = Assert.Throws<ApiException>(() => query.Normalize());
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void Negative_price_is_rejected()
    {
        var query = new ProductQuery { MinPrice = -1m };
        var exception = Assert.Throws<ApiException>(() => query.Normalize());
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void Blank_sort_defaults_to_relevance()
    {
        var query = new ProductQuery { SortBy = "   " };
        query.Normalize();
        Assert.Equal("relevance", query.SortBy);
    }
}
