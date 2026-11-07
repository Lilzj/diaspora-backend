using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.Trust;

/// <summary>
/// Trust rejected a verification.
/// <para>
/// The reason code travels and the free-text detail does not. A rejection reason written by a
/// reviewer is for a reviewer; a member deserves a sentence the template author wrote, chosen by the
/// code, which is also the only way this message can be translated at all.
/// </para>
/// </summary>
public sealed class VerificationRejectedConsumer
    : ModuleEventConsumer<VerificationRejectedIntegrationEvent>
{
    public VerificationRejectedConsumer(INotificationDispatcher dispatcher) : base(dispatcher) { }

    protected override string SourceModule => "Trust";

    protected override IEnumerable<NotificationTrigger> Translate(
        VerificationRejectedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["verificationType"] = integrationEvent.VerificationType;
        values["reasonCode"] = integrationEvent.ReasonCode;
        values["targetCountry"] = integrationEvent.TargetCountry ?? string.Empty;
        values["rejectedAt"] = integrationEvent.RejectedAt;

        // The reason and the reviewer's words, which reached nobody before 7 Nov 2026.
        values["reasontext"] = RejectionReasons.Describe(integrationEvent.ReasonCode) is { } reason
            ? $"The reason: {reason}. "
            : string.Empty;
        values["detailstext"] = string.IsNullOrWhiteSpace(integrationEvent.Details)
            ? string.Empty
            : integrationEvent.Details.Trim().TrimEnd('.') + ". ";

        yield return Trigger(
            UserId.From(integrationEvent.UserId),
            "member",
            integrationEvent.RejectedAt,
            values,
            subjectType: "verification",
            subjectId: integrationEvent.VerificationId.ToString());
    }
}

/// <summary>A rejection reason code as the member reads it.</summary>
internal static class RejectionReasons
{
    public static string? Describe(string? code) => code?.Trim().ToUpperInvariant() switch
    {
        "INVALID_DOCUMENT" => "the document could not be accepted",
        "EXPIRED_DOCUMENT" => "the document has expired",
        "MISMATCHED_NATIONALITY" => "the nationality on the document does not match the one you gave",
        "ADDITIONAL_DOCS_NEEDED" => "more documents are needed",
        "ILLEGIBLE_DOCUMENT" => "the document could not be read",
        "FACE_MATCH_FAILED" => "the photo did not match the document",
        "RESIDENCE_NOT_ESTABLISHED" => "your residence could not be confirmed",
        "NOT_DIASPORA_ELIGIBLE" => "the requirements for this verification were not met",
        "SUSPECTED_FRAUD" => "the documents could not be trusted",
        "DUPLICATE_SUBMISSION" => "it repeats a submission already made",
        null or "" => null,
        _ => null
    };
}
