namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// Error codes for a community's initiatives — 21 Oct 2026. The same <c>communities.community</c>
/// prefix as <see cref="CommunityGovernanceErrorCodes"/>: an initiative is a fact about a community.
/// </summary>
public static class CommunityInitiativeErrorCodes
{
    private const string Prefix = "communities.community.initiative";

    public const string NotFound = Prefix + ".not-found";
    public const string TitleRequired = Prefix + ".title.required";
    public const string TitleLength = Prefix + ".title.length";
    public const string DescriptionRequired = Prefix + ".description.required";
    public const string DescriptionLength = Prefix + ".description.length";
    public const string TargetDateInPast = Prefix + ".target-date.in-past";
    public const string TargetDateConflict = Prefix + ".target-date.conflict";
    public const string StatusUnknown = Prefix + ".status.unknown";
    public const string CreateStatus = Prefix + ".status.create";
    public const string StatusTransition = Prefix + ".status.transition";
    public const string Closed = Prefix + ".closed";
    public const string OrganiserRequired = Prefix + ".organiser.required";
    public const string TooManyOrganisers = Prefix + ".organiser.too-many";
    public const string OrganiserNotMember = Prefix + ".organiser.not-member";
    public const string OrganiserNotVisible = Prefix + ".organiser.not-visible";
    public const string CannotCreate = Prefix + ".cannot-create";
    public const string CannotChange = Prefix + ".cannot-change";
    public const string CannotChangeOrganisers = Prefix + ".organiser.cannot-change";
    public const string JoinMembersOnly = Prefix + ".join.members-only";
    public const string NotJoinable = Prefix + ".join.closed";
    public const string CannotPostUpdate = Prefix + ".update.cannot-post";
    public const string UpdatesClosed = Prefix + ".update.closed";
    public const string UpdateTitleRequired = Prefix + ".update.title.required";
    public const string UpdateTitleLength = Prefix + ".update.title.length";
    public const string UpdateBodyRequired = Prefix + ".update.body.required";
    public const string UpdateBodyLength = Prefix + ".update.body.length";
}
