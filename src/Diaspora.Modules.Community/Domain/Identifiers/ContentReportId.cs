using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one report of one piece of content by one member.</summary>
public sealed record ContentReportId(Guid Value) : StronglyTypedId(Value)
{
    public static ContentReportId New() => new(Guid.NewGuid());

    public static ContentReportId From(Guid value) => new(value);
}
