using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class ReviewCreateRequest
{
    [Required] public int ProductId { get; set; }

    [Range(1, 5)] public int Rating { get; set; }

    [StringLength(140)] public string? Title { get; set; }

    [StringLength(4000)] public string? Body { get; set; }

    [StringLength(120)] public string? ReviewerName { get; set; }
}
