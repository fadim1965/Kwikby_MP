using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Models;

public sealed class ApplyCouponModel
{
    [Required, StringLength(64)]
    public string Code { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}
