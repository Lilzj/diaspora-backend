using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Identifiers;

/// <summary>One member's report about an event or an organization.</summary>
public sealed record CommunityLifeReportId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityLifeReportId New() => new(Guid.NewGuid());

    public static CommunityLifeReportId From(Guid value) => new(value);
}
