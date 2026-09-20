using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// The failures the module returns through <see cref="Result"/>.
/// <para>
/// Each carries a stable <see cref="TrustErrorCodes"/> string, which becomes the
/// <c>code</c> field of the problem document. Clients branch on that string, so it is part
/// of the frozen contract — the human-readable description beside it is not, and may be
/// reworded freely.
/// </para>
/// </summary>
public static class TrustErrors
{
    public static readonly Error NotAuthenticated = Error.Unauthorized(
        TrustErrorCodes.NotAuthenticated,
        "You must be signed in to use the Trust Engine.");

    public static readonly Error VerificationNotFound = Error.NotFound(
        TrustErrorCodes.VerificationNotFound,
        "That verification request could not be found.");

    public static readonly Error VerificationDuplicate = Error.Conflict(
        TrustErrorCodes.VerificationDuplicate,
        "You already have a verification of this kind in progress.");

    public static readonly Error VerificationNotPending = Error.Conflict(
        TrustErrorCodes.VerificationNotPending,
        "That verification is not awaiting review.");

    public static readonly Error VerificationNotApproved = Error.Conflict(
        TrustErrorCodes.VerificationNotApproved,
        "That verification was not approved, so there is no outcome to apply.");

    public static readonly Error VerificationAlreadyApplied = Error.Conflict(
        TrustErrorCodes.VerificationAlreadyApplied,
        "That approval has already reached the member's standing.");

    public static readonly Error VerificationTypeUnsupported = Error.Validation(
        TrustErrorCodes.VerificationTypeUnsupported,
        "That verification type is not supported.");

    /// <summary>
    /// A module tried to submit a verification for somebody other than the acting member.
    /// <para>
    /// Added in B3.0.2 with the in-process submission contract. It cannot be reached from any
    /// HTTP route: the member's own submission takes its member from <c>ICurrentUser</c> and has
    /// nobody else to name.
    /// </para>
    /// </summary>
    public static readonly Error SubmissionNotPermitted = Error.Forbidden(
        TrustErrorCodes.SubmissionNotPermitted,
        "A verification can only be submitted for the member making the request.");

    /// <summary>
    /// A module offered evidence typed as something Trust does not recognise. B3.0.2, and
    /// unreachable from any HTTP route — the member's own wizard picks from the enum.
    /// </summary>
    public static readonly Error DocumentTypeUnsupported = Error.Validation(
        TrustErrorCodes.DocumentTypeUnsupported,
        "That document type is not one this platform recognises.");

    public static readonly Error DocumentNotFound = Error.NotFound(
        TrustErrorCodes.DocumentNotFound,
        "That document could not be found.");

    public static readonly Error DocumentExpired = Error.Validation(
        TrustErrorCodes.DocumentExpired,
        "The document has expired and cannot be used as evidence.");

    public static readonly Error AppealNotFound = Error.NotFound(
        TrustErrorCodes.AppealNotFound,
        "That appeal could not be found.");

    public static readonly Error AppealNotAllowed = Error.Conflict(
        TrustErrorCodes.AppealNotAllowed,
        "That decision cannot be appealed.");

    /// <summary>
    /// NG-42. The alternative to guessing. A decision string the reviewer's client did not
    /// spell correctly must not resolve to any outcome, least of all to the decline that a
    /// bare bool would fall through to.
    /// </summary>
    public static readonly Error AppealDecisionUnrecognised = Error.Validation(
        TrustErrorCodes.AppealDecisionUnrecognised,
        "The decision must be one of Approve, Reject or Escalate.");

    public static readonly Error AppealAlreadyOpen = Error.Conflict(
        TrustErrorCodes.AppealAlreadyOpen,
        "You already have an open appeal against that decision.");

    public static readonly Error AppealWindowClosed = Error.Conflict(
        TrustErrorCodes.AppealWindowClosed,
        "The window to appeal that decision has closed.");

    public static readonly Error SelfReviewForbidden = Error.Forbidden(
        TrustErrorCodes.SelfReviewForbidden,
        "You cannot review your own submission.");

    public static readonly Error ProfileNotFound = Error.NotFound(
        TrustErrorCodes.ProfileNotFound,
        "That member has no trust profile.");

    public static readonly Error QueueItemNotFound = Error.NotFound(
        TrustErrorCodes.QueueItemNotFound,
        "That moderation queue item could not be found.");

    public static readonly Error QueueItemNotAssigned = Error.Conflict(
        TrustErrorCodes.QueueItemNotAssigned,
        "The queue item must be assigned to you before you can decide it.");

    public static readonly Error ConfirmationSelfForbidden = Error.Forbidden(
        TrustErrorCodes.ConfirmationSelfForbidden,
        "You cannot confirm your own contribution.");

    public static readonly Error ConfirmationDuplicate = Error.Conflict(
        TrustErrorCodes.ConfirmationDuplicate,
        "You have already confirmed that contribution.");
}
