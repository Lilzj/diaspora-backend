using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Identifiers;

/// <summary>Identifies the counter row that records how one country's directory is used.</summary>
public sealed record CountryKnowledgeAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static CountryKnowledgeAnalyticsId New() => new(Guid.NewGuid());

    public static CountryKnowledgeAnalyticsId From(Guid value) => new(value);
}
