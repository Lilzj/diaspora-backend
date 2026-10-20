using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Errors;

/// <summary>What a community initiative refuses — 21 Oct 2026. Every sentence is shown as written.</summary>
public static class CommunityInitiativeErrors
{
    public static readonly Error NotFound = Error.NotFound(
        CommunityInitiativeErrorCodes.NotFound, "This community has no such initiative.");

    public static readonly Error TitleRequired = Error.Validation(
        CommunityInitiativeErrorCodes.TitleRequired, "An initiative needs a title.");

    public static readonly Error TitleLength = Error.Validation(
        CommunityInitiativeErrorCodes.TitleLength, "An initiative's title is at most 140 characters.");

    public static readonly Error DescriptionRequired = Error.Validation(
        CommunityInitiativeErrorCodes.DescriptionRequired, "Say what the initiative sets out to do.");

    public static readonly Error DescriptionLength = Error.Validation(
        CommunityInitiativeErrorCodes.DescriptionLength,
        "An initiative's description is at most 4,000 characters.");

    public static readonly Error TargetDateInPast = Error.Validation(
        CommunityInitiativeErrorCodes.TargetDateInPast, "A target date cannot be in the past.");

    public static readonly Error TargetDateConflict = Error.Validation(
        CommunityInitiativeErrorCodes.TargetDateConflict,
        "Send a new target date or clear it, not both.");

    public static readonly Error StatusUnknown = Error.Validation(
        CommunityInitiativeErrorCodes.StatusUnknown,
        "An initiative's status is Planned, Active, Completed or Cancelled.");

    public static readonly Error CreateStatus = Error.Validation(
        CommunityInitiativeErrorCodes.CreateStatus,
        "A new initiative starts as Planned or Active.");

    public static readonly Error Closed = Error.Conflict(
        CommunityInitiativeErrorCodes.Closed,
        "This initiative has been completed or cancelled and can no longer be changed.");

    public static readonly Error OrganiserRequired = Error.Validation(
        CommunityInitiativeErrorCodes.OrganiserRequired, "An initiative needs at least one organiser.");

    public static readonly Error TooManyOrganisers = Error.Validation(
        CommunityInitiativeErrorCodes.TooManyOrganisers, "An initiative has at most 10 organisers.");

    public static readonly Error OrganiserNotMember = Error.Validation(
        CommunityInitiativeErrorCodes.OrganiserNotMember,
        "Every organiser must be a current member of this community.");

    public static readonly Error OrganiserNotVisible = Error.Validation(
        CommunityInitiativeErrorCodes.OrganiserNotVisible,
        "An organiser is named to the community, so their membership must be visible to it. Ask them "
        + "to make it visible first.");

    public static readonly Error CannotCreate = Error.Forbidden(
        CommunityInitiativeErrorCodes.CannotCreate,
        "Only the people who run this community, and its ambassadors, can start an initiative.");

    public static readonly Error CannotChange = Error.Forbidden(
        CommunityInitiativeErrorCodes.CannotChange,
        "Only this initiative's organisers, or the people who run the community, can change it.");

    public static readonly Error CannotChangeOrganisers = Error.Forbidden(
        CommunityInitiativeErrorCodes.CannotChangeOrganisers,
        "Only the people who run this community, and its ambassadors, can change who organises an "
        + "initiative.");

    public static readonly Error JoinMembersOnly = Error.Forbidden(
        CommunityInitiativeErrorCodes.JoinMembersOnly,
        "Join the community first to take part in its initiatives.");

    public static readonly Error NotJoinable = Error.Conflict(
        CommunityInitiativeErrorCodes.NotJoinable,
        "This initiative has been completed or cancelled and is no longer taking part.");

    public static readonly Error CannotPostUpdate = Error.Forbidden(
        CommunityInitiativeErrorCodes.CannotPostUpdate,
        "Only this initiative's organisers, or the people who run the community, can post updates.");

    public static readonly Error UpdatesClosed = Error.Conflict(
        CommunityInitiativeErrorCodes.UpdatesClosed,
        "This initiative was cancelled, so no further updates can be posted.");

    public static readonly Error UpdateTitleRequired = Error.Validation(
        CommunityInitiativeErrorCodes.UpdateTitleRequired, "An update needs a title.");

    public static readonly Error UpdateTitleLength = Error.Validation(
        CommunityInitiativeErrorCodes.UpdateTitleLength, "An update's title is at most 140 characters.");

    public static readonly Error UpdateBodyRequired = Error.Validation(
        CommunityInitiativeErrorCodes.UpdateBodyRequired, "Say what has happened.");

    public static readonly Error UpdateBodyLength = Error.Validation(
        CommunityInitiativeErrorCodes.UpdateBodyLength, "An update is at most 4,000 characters.");

    public static Error StatusTransition(CommunityInitiativeStatus from, CommunityInitiativeStatus to) =>
        Error.Conflict(
            CommunityInitiativeErrorCodes.StatusTransition,
            $"An initiative that is {from} cannot become {to}. Planned moves to Active or Cancelled, "
            + "and Active to Completed or Cancelled.");
}
