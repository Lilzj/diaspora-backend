namespace Diaspora.Modules.Financial.Domain.Webhooks;

/// <summary>The record of every provider callback this deployment has received.</summary>
public interface IProcessedWebhookEventRepository
{
    void Add(ProcessedWebhookEvent record);

    /// <summary>
    /// The stored outcome for a provider's event identifier, or null when it is new.
    /// <para>
    /// Consults what is already staged in the unit of work as well as the database, because two
    /// legs of one request can record the same event and only one of them has been saved.
    /// </para>
    /// </summary>
    Task<ProcessedWebhookEvent?> FindAsync(
        string provider, string providerEventId, CancellationToken cancellationToken = default);

    /// <summary>Recent callbacks for one provider, newest first. The support surface.</summary>
    Task<IReadOnlyList<ProcessedWebhookEvent>> ListRecentAsync(
        string? provider, int take, CancellationToken cancellationToken = default);
}
