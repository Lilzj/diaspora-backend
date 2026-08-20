using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>
/// Identifies one country's intelligence usage counters.
/// <para>
/// Separate from <see cref="CountryKnowledgeAnalyticsId"/> because the two answer different
/// editorial questions. Knowledge analytics say whether the guides for a country are being read;
/// these say whether people saw the advisory in time. Folding them into one row would make a busy
/// month of guide reading hide a month in which nobody opened a single emergency number.
/// </para>
/// </summary>
public sealed record CountryIntelligenceAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryIntelligenceAnalyticsId New() => new(Guid.NewGuid());

    public static CountryIntelligenceAnalyticsId From(Guid value) => new(value);
}
