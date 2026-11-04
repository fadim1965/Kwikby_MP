namespace kwikby_MP.Api.Models.Responses;

public sealed class AuditLogResponse
{
    public int Id { get; set; }

    public string Actor { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int? EntityId { get; set; }

    public string? Summary { get; set; }

    public DateTime CreatedUtc { get; set; }
}
