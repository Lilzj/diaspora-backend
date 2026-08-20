namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// What a verification provider did with a claim.
/// <para>
/// <b>There is no <c>Granted</c>, and its absence is ADR-002.</b> Trust owns the decision; this
/// module records that a claim was raised and learns the answer from a published integration event.
/// A provider that could grant would be a second path to a verified badge, and the whole point of a
/// single inbound projection is that there is exactly one.
/// </para>
/// <para>
/// The case identifier is the only thing that has to come back, and it has to: without it, the
/// decision Trust eventually publishes has nothing to correlate against. B8 shipped without storing
/// the equivalent and every approval landed nowhere.
/// </para>
/// </summary>
public sealed record AuthorityVerificationOutcome
{
    private AuthorityVerificationOutcome(Guid caseId) => CaseId = caseId;

    /// <summary>The case a reviewer will work. Stored on the subject, correlated on inbound.</summary>
    public Guid CaseId { get; }

    /// <summary>A claim was raised and is with a reviewer.</summary>
    public static AuthorityVerificationOutcome Deferred(Guid caseId) => new(caseId);
}
