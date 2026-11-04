namespace kwikby_MP.Api.Models.Responses;

public sealed class ReviewResponse
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public string? ProductName { get; set; }

    public string? UserId { get; set; }

    public string? UserEmail { get; set; }

    public int Rating { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public int Status { get; set; }

    public DateTime CreatedUtc { get; set; }
}
