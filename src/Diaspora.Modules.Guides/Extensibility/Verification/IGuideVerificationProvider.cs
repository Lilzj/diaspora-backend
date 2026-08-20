using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Extensibility.Verification;

/// <summary>
/// How a guide verification claim actually reaches whoever decides it.
/// <para>
/// <b>The seam exists because "verified" does not have to mean Trust's review queue.</b> B8's
/// shipped provider routes every claim there, which is right for a general deployment. But a
/// deployment with an editorial board, a partner NGO's fact-checkers, or a government content
/// programme can decide guide accuracy through its own channel — and none of that belongs in
/// B8, which must not know what a fact-checking API looks like.
/// </para>
/// <para>
/// <b>What a provider may not do:</b> grant the mark. It returns an outcome, and B8 applies it
/// through the aggregate's own projection path — the only route to a verified badge, kept
/// single deliberately.
/// </para>
/// <para>
/// Providers run in <see cref="Order"/>; the registry takes the first that returns a decision.
/// A provider that cannot decide returns <see cref="GuideVerificationOutcome.Undecided"/>
/// rather than throwing, so an unreachable service falls through to the next provider instead
/// of failing the submission.
/// </para>
/// </summary>
public interface IGuideVerificationProvider
{
    /// <summary>The module contributing. Appears in telemetry and the decision's audit trail.</summary>
    string Module { get; }

    /// <summary>Lower runs first. B8's Trust-backed provider is deliberately last, at 1000.</summary>
    int Order => 100;

    /// <summary>
    /// Attempts to lodge or decide the claim.
    /// <para>
    /// A failed <see cref="Result"/> means the provider itself broke — an API was down, a
    /// credential expired — and the registry moves on. An
    /// <see cref="GuideVerificationOutcome.Undecided"/> success means the provider worked and
    /// has no opinion, which is a different thing and is also not an error.
    /// </para>
    /// </summary>
    Task<Result<GuideVerificationOutcome>> SubmitAsync(
        GuideVerificationRequest request, CancellationToken cancellationToken = default);
}
