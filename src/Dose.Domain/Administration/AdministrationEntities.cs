using Dose.Domain.Common;

namespace Dose.Domain.Administration;

/// <summary>Admin-editable operational setting (durations, deadlines, limits). Key/value.</summary>
public sealed class SystemSetting
{
    public required string Key { get; init; }
    public required string Value { get; set; }
    public string? DescriptionAr { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class AuditLog : Entity
{
    public required string Actor { get; init; }
    public required string Entity { get; init; }
    public required string EntityId { get; init; }
    public required string Action { get; init; }
    public string? DataJson { get; init; }
    public DateTime AtUtc { get; init; }
}
