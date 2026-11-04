using System.ComponentModel.DataAnnotations;

namespace kwikby_MP.Api.Models.Requests;

public sealed class CategoryCreateRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 1)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int? ParentId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 1)]
    public decimal? CommissionRate { get; set; }

    public bool IsGlobal { get; set; } = true;

    public int? ShopId { get; set; }
}

public sealed class CategoryUpdateRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 1)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public int? ParentId { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 1)]
    public decimal? CommissionRate { get; set; }
}
