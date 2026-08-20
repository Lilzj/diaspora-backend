namespace Diaspora.Modules.CommunityLife.Extensibility.Search;

/// <summary>One organization as a search engine returns it. Identifier and score only.</summary>
public sealed record OrganizationSearchHit(Guid OrganizationId, double Score);
