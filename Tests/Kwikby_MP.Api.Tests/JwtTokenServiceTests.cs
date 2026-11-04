using System.IdentityModel.Tokens.Jwt;
using kwikby_MP.Api.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kwikby_MP.Api.Tests;

public sealed class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int minutes = 15) =>
        new(Options.Create(new ApiOptions
        {
            Jwt = new JwtOptions
            {
                Issuer = "kwikby-tests",
                Audience = "kwikby-tests-api",
                SigningKey = new string('k', 32),
                AccessTokenMinutes = minutes
            }
        }));

    [Fact]
    public void Token_carries_the_expected_claims()
    {
        var service = CreateService();
        var result = service.Create(new TokenSubject(
            "user-1",
            "alice@example.com",
            "Alice",
            new[] { "Admin", "Customer" },
            new[] { 7, 9 }));

        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(900, result.ExpiresIn);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal("kwikby-tests", token.Issuer);
        Assert.Equal("kwikby-tests-api", token.Audiences.Single());
        Assert.Equal("user-1", token.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal("alice@example.com", token.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal("Alice", token.Claims.Single(c => c.Type == "name").Value);
        Assert.Equal(
            new[] { "Admin", "Customer" },
            token.Claims.Where(c => c.Type == "role").Select(c => c.Value).OrderBy(v => v).ToArray());
        Assert.Equal(
            new[] { "7", "9" },
            token.Claims.Where(c => c.Type == "shop_id").Select(c => c.Value).OrderBy(v => v).ToArray());
    }

    [Fact]
    public void Every_token_has_a_unique_jti()
    {
        var service = CreateService();
        var subject = new TokenSubject("user-1", "alice@example.com", "Alice", Array.Empty<string>(), Array.Empty<int>());

        var first = new JwtSecurityTokenHandler().ReadJwtToken(service.Create(subject).AccessToken);
        var second = new JwtSecurityTokenHandler().ReadJwtToken(service.Create(subject).AccessToken);

        Assert.NotEqual(
            first.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value,
            second.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    [Fact]
    public void Expiry_matches_the_configured_minutes()
    {
        var service = CreateService(minutes: 2);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(service.Create(
            new TokenSubject("u", "e", "d", Array.Empty<string>(), Array.Empty<int>())).AccessToken);

        Assert.InRange((token.ValidTo - token.ValidFrom).TotalMinutes, 1.9, 2.1);
    }
}
