namespace Dose.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}

/// <summary>A user-facing text in both supported languages.</summary>
public readonly record struct LocalizedText(string Ar, string En)
{
    public string For(string? language) => language == "en" ? En : Ar;
}

/// <summary>
/// Thrown when a domain rule is violated. Carries a stable machine-readable code (clients may override the text)
/// and the message in Arabic and English; the API returns the one matching the request language.
/// </summary>
public sealed class DomainException(string code, string ar, string en) : Exception(en)
{
    public DomainException(string code, LocalizedText text) : this(code, text.Ar, text.En) { }

    public string Code { get; } = code;
    public LocalizedText Text { get; } = new(ar, en);
}
