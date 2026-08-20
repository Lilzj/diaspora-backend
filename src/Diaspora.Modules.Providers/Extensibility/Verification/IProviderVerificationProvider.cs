using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Extensibility.Verification;

/// <summary>
/// How a provider verification claim actually reaches whoever decides it.
/// <para>
/// <b>The seam exists because "verified" does not mean one thing.</b> B7's shipped provider
/// routes every kind to Trust, which is right for identity, business registration and
/// organizational legitimacy. But a deployment that integrates a national companies register
/// can verify a company number in a second without a human, and a professional body's API can
/// confirm a solicitor's practising certificate. Both are better answers than a queue, and
/// neither belongs in B7 — a module that knew about Companies House would be a module that has
/// to know about every other country's equivalent.
/// </para>
/// <para>
/// <b>What a provider may not do:</b> grant a badge. It returns an outcome, and B7 applies it
/// through the aggregate's own projection path, which raises the event and recomputes the
/// rollup. That keeps ADR-002's line intact even for an automated verifier.
/// </para>
/// <para>
/// Several providers may claim the same kind; the registry runs them in <see cref="Order"/> and
/// takes the first that returns a decision. A provider that cannot decide returns
/// <see cref="ProviderVerificationOutcome.Undecided"/> rather than throwing, so an unreachable
/// registry API falls through to the next provider — and ultimately to Trust — instead of
/// failing the submission.
/// </para>
/// </summary>
public interface IProviderVerificationProvider
{
    /// <summary>The module contributing. Appears in telemetry and in the decision's audit trail.</summary>
    string Module { get; }

    /// <summary>Lower runs first. B7's Trust-backed provider is deliberately last, at 1000.</summary>
    int Order => 100;

    /// <summary>Whether this provider handles the kind at all. A cheap synchronous check.</summary>
    bool Handles(ProviderVerificationKind kind);

    /// <summary>
    /// Attempts to decide the claim.
    /// <para>
    /// A failed <see cref="Result"/> means the provider itself broke — an API was down, a
    /// credential expired — and the registry moves on. An
    /// <see cref="ProviderVerificationOutcome.Undecided"/> success means the provider worked and
    /// has no opinion, which is a different thing and is also not an error.
    /// </para>
    /// </summary>
    Task<Result<ProviderVerificationOutcome>> SubmitAsync(
        ProviderVerificationRequest request, CancellationToken cancellationToken = default);
}
