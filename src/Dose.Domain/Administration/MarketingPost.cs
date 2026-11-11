using Dose.Domain.Common;

namespace Dose.Domain.Administration;

/// <summary>
/// One social post from the marketing queue (marketing/publish/queue.json) and what happened when it was published.
/// The queue key is the agency's item id (e.g. "w02-03"), so a post is never published twice.
/// </summary>
public sealed class MarketingPost : Entity
{
    private MarketingPost() { }

    public MarketingPost(string key, string channel, DateTime nowUtc)
    {
        Key = key;
        Channel = channel;
        CreatedAtUtc = nowUtc;
    }

    public string Key { get; private set; } = "";
    /// <summary>"facebook" for now; "instagram" later.</summary>
    public string Channel { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public string? ExternalId { get; private set; }
    public string? LastError { get; private set; }
    public int Attempts { get; private set; }

    public void Published(string externalId, DateTime nowUtc)
    {
        ExternalId = externalId;
        PublishedAtUtc = nowUtc;
        LastError = null;
        Attempts++;
    }

    public void Failed(string error)
    {
        LastError = error.Length > 1000 ? error[..1000] : error;
        Attempts++;
    }
}
