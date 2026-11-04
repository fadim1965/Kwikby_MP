using kwikby_MP.Api.Infrastructure;
using kwikby_MP.Api.Models.Requests;
using kwikby_MP.Api.Models.Responses;
using kwikby_MP.Api.Repositories;
using kwikby_MP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace kwikby_MP.Api.Controllers;

[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    // Same dummy hash the MVC AccountService uses, so an unknown email and a wrong
    // password take roughly the same time and cannot be distinguished.
    private const string DummyHash =
        "210000.AAAAAAAAAAAAAAAAAAAAAA=.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    private readonly IAuthRepository _auth;
    private readonly IPasswordHasher _hasher;
    private readonly JwtTokenService _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthRepository auth,
        IPasswordHasher hasher,
        JwtTokenService tokens,
        ILogger<AuthController> logger)
    {
        _auth = auth;
        _hasher = hasher;
        _tokens = tokens;
        _logger = logger;
    }

    /// <summary>Exchange an email and password for a JWT access token.</summary>
    [AllowAnonymous]
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Token(
        [FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        var user = await _auth.GetUserForLoginAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _hasher.Verify(request.Password, DummyHash);
            return UnauthorizedProblem("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("API sign-in blocked for deactivated account {UserId}", user.Id);
            return UnauthorizedProblem("Invalid email or password.");
        }

        if (user.IsLockedOut)
        {
            _logger.LogWarning("API sign-in blocked for locked-out account {UserId}", user.Id);
            return UnauthorizedProblem("Invalid email or password.");
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            await _auth.RecordFailedLoginAsync(request.Email, cancellationToken);
            return UnauthorizedProblem("Invalid email or password.");
        }

        await _auth.RecordLoginAsync(user.Id, cancellationToken);

        var token = _tokens.Create(new TokenSubject(
            user.Id, user.Email, user.DisplayName, user.Roles, user.ShopIds));

        return Ok(new TokenResponse
        {
            AccessToken = token.AccessToken,
            TokenType = token.TokenType,
            ExpiresIn = token.ExpiresIn
        });
    }

    /// <summary>The claims carried by the current token, refreshed from the database.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return UnauthorizedProblem("The token does not contain a subject claim.");
        }

        var claims = await _auth.GetClaimsAsync(userId, cancellationToken);
        if (claims is null || !claims.IsActive)
        {
            return UnauthorizedProblem("The account is no longer active.");
        }

        return Ok(new MeResponse
        {
            UserId = claims.UserId,
            Email = claims.Email,
            DisplayName = claims.DisplayName,
            Roles = claims.Roles,
            ShopIds = claims.ShopIds
        });
    }
}
