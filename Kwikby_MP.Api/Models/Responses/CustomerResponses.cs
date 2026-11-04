namespace kwikby_MP.Api.Models.Responses;

public sealed class CustomerResponse
{
    public string UserId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public int OrderCount { get; set; }

    public decimal TotalSpent { get; set; }
}
