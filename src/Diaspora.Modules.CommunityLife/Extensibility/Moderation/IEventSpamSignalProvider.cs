namespace Diaspora.Modules.CommunityLife.Extensibility.Moderation;

/// <summary>
/// How a module or a deployment contributes spam detection.
/// <para>
/// <b>The brief asks for "spam detection hooks" and this is the hook.</b> Nothing sophisticated
/// ships: the module registers a duplicate-title provider and a volume provider, both of which are
/// arithmetic. What matters is that a real detector — a text classifier, a link-reputation service,
/// a shared blocklist — is a class in whichever assembly owns it, and lands without a change here.
/// </para>
/// <para>
/// <b>Advisory, never authoritative.</b> A provider raises signals; the composition rule in
/// <see cref="SpamAssessment"/> decides, and a moderator decides after that. A provider that could
/// refuse a publication outright would be a piece of third-party code with the power to silence a
/// community.
/// </para>
/// </summary>
public interface IEventSpamSignalProvider
{
    string Name { get; }

    /// <summary>
    /// The signals this provider raises about an event, or none.
    /// <para>
    /// Return quickly or return nothing: this runs inside the publish path and a slow provider
    /// delays an organizer pressing a button. Throwing is not a failure of publication — the
    /// assessor isolates each provider — but it does cost that provider's opinion.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<EventSpamSignal>> AssessAsync(
        EventSpamContext context, CancellationToken cancellationToken = default);
}
