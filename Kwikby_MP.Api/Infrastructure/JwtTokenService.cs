using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace kwikby_MP.Api.Infrastructure;

/// <summary>The identity facts copied into the JWT.</summary>
public sealed record TokenSubject(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<int> ShopIds);

public sealed record TokenResult(string AccessToken, string TokenType, int ExpiresIn);

public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<ApiOptions> options) => _options = options.Value.Jwt;

    public TokenResult Create(TokenSubject subject)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.UserId),
            new(JwtRegisteredClaimNames.Email, subject.Email),
            new("name", subject.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(subject.Roles.Select(role => new Claim("role", role)));
        claims.AddRange(subject.ShopIds.Select(id => new Claim("shop_id", id.ToString())));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: credentials);

        return new TokenResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            checked(_options.AccessTokenMinutes * 60));
    }
}
