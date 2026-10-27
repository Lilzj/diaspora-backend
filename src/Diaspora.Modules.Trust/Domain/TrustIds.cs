using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// The identifiers the Trust module owns.
/// <para>
/// Declared in the module rather than the SharedKernel's shared list, for the reason the
/// Bookmarks reference module gives: a module should not need a platform-wide file edited
/// to introduce an entity. The trade — registering each value converter in the module's own
/// entity configurations — is paid in <c>Persistence/</c>.
/// </para>
/// <para>
/// Note that <c>VerificationId</c> already exists in the SharedKernel from B1. This module
/// deliberately does <b>not</b> reuse it: that id was minted for Identity's notion of a
/// verification request, and a shared id type between two modules is exactly the coupling
/// ADR-002 spent B2 removing. Trust mints <see cref="VerificationRequestId"/> instead.
/// </para>
/// </summary>
public sealed record VerificationRequestId(Guid Value) : StronglyTypedId(Value)
{
    public static VerificationRequestId New() => new(Guid.NewGuid());

    public static VerificationRequestId From(Guid value) => new(value);
}

/// <summary>A document attached to a verification request.</summary>
public sealed record VerificationDocumentId(Guid Value) : StronglyTypedId(Value)
{
    public static VerificationDocumentId New() => new(Guid.NewGuid());

    public static VerificationDocumentId From(Guid value) => new(value);
}

/// <summary>One reviewer's decision on a verification request.</summary>
public sealed record VerificationReviewId(Guid Value) : StronglyTypedId(Value)
{
    public static VerificationReviewId New() => new(Guid.NewGuid());

    public static VerificationReviewId From(Guid value) => new(value);
}

/// <summary>A member's trust profile — the aggregate that holds their standing.</summary>
public sealed record TrustProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static TrustProfileId New() => new(Guid.NewGuid());

    public static TrustProfileId From(Guid value) => new(value);
}

/// <summary>A badge awarded to a member.</summary>
public sealed record BadgeAwardId(Guid Value) : StronglyTypedId(Value)
{
    public static BadgeAwardId New() => new(Guid.NewGuid());

    public static BadgeAwardId From(Guid value) => new(value);
}

/// <summary>A member's standing within one country community.</summary>
public sealed record CountryStandingId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryStandingId New() => new(Guid.NewGuid());

    public static CountryStandingId From(Guid value) => new(value);
}

/// <summary>A member's reputation profile.</summary>
public sealed record ReputationProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static ReputationProfileId New() => new(Guid.NewGuid());

    public static ReputationProfileId From(Guid value) => new(value);
}

/// <summary>One scoring event against a reputation profile.</summary>
public sealed record ReputationEventId(Guid Value) : StronglyTypedId(Value)
{
    public static ReputationEventId New() => new(Guid.NewGuid());

    public static ReputationEventId From(Guid value) => new(value);
}

/// <summary>An appeal against a trust decision.</summary>
public sealed record AppealId(Guid Value) : StronglyTypedId(Value)
{
    public static AppealId New() => new(Guid.NewGuid());

    public static AppealId From(Guid value) => new(value);
}

/// <summary>One reviewer's decision on an appeal.</summary>
public sealed record AppealReviewId(Guid Value) : StronglyTypedId(Value)
{
    public static AppealReviewId New() => new(Guid.NewGuid());

    public static AppealReviewId From(Guid value) => new(value);
}

/// <summary>An entry on a member's trust timeline.</summary>
public sealed record TrustEventId(Guid Value) : StronglyTypedId(Value)
{
    public static TrustEventId New() => new(Guid.NewGuid());

    public static TrustEventId From(Guid value) => new(value);
}

/// <summary>An item in the moderation queue.</summary>
public sealed record ModerationItemId(Guid Value) : StronglyTypedId(Value)
{
    public static ModerationItemId New() => new(Guid.NewGuid());

    public static ModerationItemId From(Guid value) => new(value);
}

/// <summary>One moderator decision recorded against a queue item.</summary>
public sealed record ModerationDecisionId(Guid Value) : StronglyTypedId(Value)
{
    public static ModerationDecisionId New() => new(Guid.NewGuid());

    public static ModerationDecisionId From(Guid value) => new(value);
}

/// <summary>A community confirmation recorded against a contribution.</summary>
public sealed record CommunityContributionId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityContributionId New() => new(Guid.NewGuid());

    public static CommunityContributionId From(Guid value) => new(value);
}

/// <summary>One piece of evidence counted toward a member's risk (RWD-14, CS-4b).</summary>
public sealed record RiskSignalId(Guid Value) : StronglyTypedId(Value)
{
    public static RiskSignalId New() => new(Guid.NewGuid());

    public static RiskSignalId From(Guid value) => new(value);
}
