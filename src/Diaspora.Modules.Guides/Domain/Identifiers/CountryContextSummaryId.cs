using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies the cross-vertical counter projection for one country.
/// <para>
/// The row behind the country dashboard's contextual band — how many jobs, homes, listings and
/// events the platform holds for this country. Maintained entirely from other verticals'
/// published integration contracts, and holding counts and nothing else: no title, no
/// identifier, nothing that would make it a second copy of somebody else's content.
/// </para>
/// </summary>
public sealed record CountryContextSummaryId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryContextSummaryId New() => new(Guid.NewGuid());

    public static CountryContextSummaryId From(Guid value) => new(value);
}
