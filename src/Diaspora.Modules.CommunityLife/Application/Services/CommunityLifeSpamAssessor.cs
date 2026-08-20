using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Extensibility.Moderation;
using Diaspora.Modules.CommunityLife.Observability;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Runs every registered spam-signal provider and composes what they said.
/// <para>
/// <b>A provider that throws or hangs costs its own opinion and nothing else.</b> This runs inside
/// the publish path, so a slow detector delays an organizer pressing a button; isolating each one is
/// what keeps a third-party classifier from being able to stop a community announcing a service.
/// </para>
/// <para>
/// The composition rule lives on <see cref="SpamAssessment"/> and is deliberately conservative: one
/// severe signal holds, two warnings flag, anything else is a note. The cost of a false positive
/// here is a church unable to announce a service.
/// </para>
/// </summary>
public sealed class CommunityLifeSpamAssessor
{
    private readonly IReadOnlyList<IEventSpamSignalProvider> _providers;
    private readonly ILogger<CommunityLifeSpamAssessor> _logger;

    public CommunityLifeSpamAssessor(
        IEnumerable<IEventSpamSignalProvider> providers,
        ILogger<CommunityLifeSpamAssessor> logger)
    {
        _providers = [.. providers];
        _logger = logger;
    }

    public bool HasProviders => _providers.Count > 0;

    /// <summary>Assesses an event about to be published.</summary>
    public async Task<SpamAssessment> AssessAsync(
        CommunityEvent communityEvent,
        int organizerLiveEventCount,
        bool isOrganizerVerified,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        if (_providers.Count == 0)
        {
            return SpamAssessment.Clean;
        }

        var context = new EventSpamContext
        {
            EventId = communityEvent.Id.Value,
            OrganizerUserId = communityEvent.OrganizerUserId.Value,
            OrganizationId = communityEvent.OrganizationId?.Value,
            Title = communityEvent.Title.Value,
            Summary = communityEvent.Description.Summary,
            Body = communityEvent.Description.Body,
            CategoryKey = communityEvent.CategoryKey,
            CountryCode = communityEvent.Location.CountryCode,
            VenueName = communityEvent.Venue.Name,
            StartsAt = communityEvent.Schedule.StartsAt,
            OrganizerLiveEventCount = organizerLiveEventCount,
            IsOrganizerVerified = isOrganizerVerified,
            HasMedia = communityEvent.MediaReferences.Count > 0,
            ExternalUrls =
            [
                .. new[]
                    {
                        communityEvent.Contact.Website,
                        communityEvent.Venue.OnlineUrl
                    }
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Select(url => url!)
            ]
        };

        var signals = new List<EventSpamSignal>();

        foreach (var provider in _providers)
        {
            try
            {
                signals.AddRange(await provider.AssessAsync(context, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // A contributed detector must not stop a community publishing.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Spam provider {Provider} failed for event {EventId}. Skipping it.",
                    provider.Name,
                    communityEvent.Id.Value);
            }
#pragma warning restore CA1031
        }

        foreach (var signal in signals)
        {
            CommunityLifeTelemetry.SpamSignalsRaised.Add(
                1,
                new KeyValuePair<string, object?>("severity", signal.Severity.ToString()),
                new KeyValuePair<string, object?>("source", signal.Source ?? signal.Code));
        }

        return new SpamAssessment { Signals = signals };
    }
}
