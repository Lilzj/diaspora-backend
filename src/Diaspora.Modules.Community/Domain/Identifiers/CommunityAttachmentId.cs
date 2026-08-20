using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one media attachment on a post or a comment.</summary>
public sealed record CommunityAttachmentId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityAttachmentId New() => new(Guid.NewGuid());

    public static CommunityAttachmentId From(Guid value) => new(value);
}
