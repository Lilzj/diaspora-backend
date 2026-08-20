using Diaspora.Modules.Trust.Domain;

namespace Diaspora.Modules.Trust.Extensibility;

/// <summary>
/// Everything type-specific about one kind of verification.
/// <para>
/// The seam that keeps <c>SubmitVerificationCommandHandler</c> from growing a switch that
/// gets a new arm every time the platform learns to verify something else. A handler owns
/// what evidence its type needs, whether a rule can decide it without a human, how long the
/// grant lasts, and what it is worth — and the pipeline asks rather than knows.
/// </para>
/// <para>
/// Implementations are discovered from any loaded <c>Diaspora.*</c> assembly, so a future
/// Marketplace module can add "Seller Verification" without a line changing in Trust. Two
/// handlers claiming the same <see cref="Type"/> fail at startup rather than letting
/// registration order decide which one runs.
/// </para>
/// </summary>
public interface IVerificationTypeHandler
{
    /// <summary>The type this handler owns.</summary>
    VerificationType Type { get; }

    /// <summary>
    /// Document kinds that satisfy this type. Empty means the type needs no documents —
    /// phone and email are verified by possession, not by paperwork.
    /// </summary>
    IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments { get; }

    /// <summary>Whether at least one document must be attached before the request may queue.</summary>
    bool RequiresDocument { get; }

    /// <summary>Whether a biometric selfie is needed for a face match.</summary>
    bool RequiresSelfie { get; }

    /// <summary>
    /// How long a grant of this type lasts. Null means it does not lapse on its own.
    /// <para>
    /// Residency lapses because permits do. Identity does not — a person's identity is not
    /// re-checked annually — so its handler returns null.
    /// </para>
    /// </summary>
    TimeSpan? GrantLifetime { get; }

    /// <summary>How long review should take. Drives the overdue queue.</summary>
    TimeSpan ReviewSla { get; }

    /// <summary>
    /// Whether this type may be granted without a human ever seeing it. Types that gate
    /// publishing should return false — an automatic path into contributor status is an
    /// automatic path for anyone who finds it.
    /// </summary>
    bool SupportsAutomaticApproval { get; }

    /// <summary>
    /// Checks a submission before it enters the queue.
    /// <para>
    /// Returns the problems found. An empty result means the submission is well-formed —
    /// not that it is truthful, which is what review is for.
    /// </para>
    /// </summary>
    IReadOnlyCollection<string> Validate(VerificationRequest request);

    /// <summary>
    /// Offers a decision without a human.
    /// <para>
    /// Returning <see cref="AutomaticDecision.Defer"/> — which every conservative handler
    /// should default to — sends the request to the queue.
    /// </para>
    /// </summary>
    AutomaticDecision Evaluate(VerificationRequest request, DateTimeOffset asOf);

    /// <summary>
    /// What being granted this type is worth in reputation. Null awards nothing.
    /// </summary>
    ReputationAward? ApprovalAward { get; }
}

/// <summary>
/// A handler's verdict when asked whether it can decide without a human.
/// <para>
/// A three-way answer rather than a bool, because "I refuse this" and "I have no opinion"
/// are different outcomes and collapsing them means every uncertain case gets auto-rejected
/// or every certain fraud gets queued.
/// </para>
/// </summary>
public sealed record AutomaticDecision
{
    public required AutomaticDecisionKind Kind { get; init; }

    /// <summary>Why. Required for a rejection; shown to the member.</summary>
    public VerificationRejectionReason? Reason { get; init; }

    public string? Details { get; init; }

    /// <summary>The rule's name, recorded against the review so the decision is attributable.</summary>
    public string RuleName { get; init; } = "unnamed-rule";

    /// <summary>Send it to a human.</summary>
    public static AutomaticDecision Defer { get; } = new() { Kind = AutomaticDecisionKind.Defer };

    public static AutomaticDecision Approve(string ruleName, string? details = null) => new()
    {
        Kind = AutomaticDecisionKind.Approve,
        RuleName = ruleName,
        Details = details
    };

    public static AutomaticDecision Reject(
        string ruleName,
        VerificationRejectionReason reason,
        string? details = null) => new()
        {
            Kind = AutomaticDecisionKind.Reject,
            RuleName = ruleName,
            Reason = reason,
            Details = details
        };
}

public enum AutomaticDecisionKind
{
    /// <summary>No opinion — a human decides.</summary>
    Defer = 0,

    Approve = 1,

    Reject = 2
}
