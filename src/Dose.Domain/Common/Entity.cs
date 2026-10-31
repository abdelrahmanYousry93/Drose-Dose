namespace Dose.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>Thrown when a domain rule is violated. Carries a stable machine-readable code.</summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
