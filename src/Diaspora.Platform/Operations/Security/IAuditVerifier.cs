namespace Diaspora.Platform.Operations.Security;

/// <summary>What an audit verification found.</summary>
/// <param name="Area">What was verified — <c>financial</c>, <c>permissions</c>, <c>administration</c>.</param>
/// <param name="Module">Who owns it.</param>
/// <param name="RecordsExamined">How many audit records were read.</param>
/// <param name="ExpectedRecords">
/// How many the verifier expected, when it can know. The two numbers together are the finding: a
/// verifier that only counted what exists can never detect what is missing.
/// </param>
/// <param name="Gaps">Specific records that should exist and do not.</param>
/// <param name="MutationsDetected">
/// Records that changed after they were written. An audit trail that can be edited is not an audit
/// trail, so any number here other than zero is the most serious finding this can produce.
/// </param>
/// <param name="VerifiedThrough">How far back the verification reached.</param>
public sealed record AuditVerificationResult(
    string Area,
    string Module,
    long RecordsExamined,
    long? ExpectedRecords,
    IReadOnlyList<string> Gaps,
    long MutationsDetected,
    DateTimeOffset VerifiedThrough)
{
    /// <summary>Whether anything was found.</summary>
    public bool IsClean => Gaps.Count == 0 && MutationsDetected == 0;
}

/// <summary>
/// Checks that the audit trail says what it should.
/// <para>
/// <b>Auditing that is never verified is auditing nobody has tested.</b> Every module on this platform
/// writes audit records; nothing until now has ever read them back and asked whether the set is
/// complete. The failure mode is quiet and total — a handler that stopped writing its audit record two
/// releases ago leaves a trail that looks fine until somebody needs the records that were never written.
/// </para>
/// <para>
/// Implemented per area by whoever owns the data. Only the Financial Platform can say how many ledger
/// entries should have an audit stamp; only Identity can say which administrative actions should have
/// produced a record.
/// </para>
/// </summary>
public interface IAuditVerifier
{
    /// <summary>What this verifies.</summary>
    string Area { get; }

    /// <summary>The owning module.</summary>
    string Module { get; }

    /// <summary>
    /// Verifies the trail from <paramref name="since"/> to now.
    /// </summary>
    Task<AuditVerificationResult> VerifyAsync(
        DateTimeOffset since, CancellationToken cancellationToken = default);
}
