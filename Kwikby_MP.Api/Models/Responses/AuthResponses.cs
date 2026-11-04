namespace kwikby_MP.Api.Models.Responses;

public sealed class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public int ExpiresIn { get; set; }
}

public sealed class MeResponse
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyList<int> ShopIds { get; set; } = Array.Empty<int>();
}
