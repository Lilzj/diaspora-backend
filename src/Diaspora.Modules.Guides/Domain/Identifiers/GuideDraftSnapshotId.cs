using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies one auto-saved draft snapshot of a guide.</summary>
public sealed record GuideDraftSnapshotId(Guid Value) : StronglyTypedId(Value)
{
    public static GuideDraftSnapshotId New() => new(Guid.NewGuid());

    public static GuideDraftSnapshotId From(Guid value) => new(value);
}
