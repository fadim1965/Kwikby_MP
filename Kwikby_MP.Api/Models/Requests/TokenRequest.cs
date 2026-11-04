using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class TokenRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}
