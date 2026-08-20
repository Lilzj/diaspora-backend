namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// How and where the work is performed, and what the employer will do to get someone there.
/// <para>
/// The three fields together answer the question a diaspora candidate asks first: can I,
/// specifically, do this job from where I am? Everything here is the employer's own statement —
/// nothing in this module verifies immigration claims.
/// </para>
/// </summary>
public sealed record JobWorkArrangementResponse
{
    /// <summary><c>OnSite</c>, <c>Hybrid</c> or <c>Remote</c>.</summary>
    public required string WorkplaceType { get; init; }

    public required bool IsRemote { get; init; }

    public required bool OffersVisaSponsorship { get; init; }

    public required bool OffersRelocationAssistance { get; init; }

    /// <summary>Days in the office. Present only for a hybrid arrangement.</summary>
    public int? OnSiteDaysPerWeek { get; init; }

    /// <summary>"within the EU", "must overlap 09:00–13:00 UTC". The most useful line on a
    /// remote posting and the one most often absent.</summary>
    public string? RemotePolicy { get; init; }
}
