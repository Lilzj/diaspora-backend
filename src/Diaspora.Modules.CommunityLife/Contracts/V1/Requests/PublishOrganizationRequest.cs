namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Publish an organization.</summary>
public sealed record PublishOrganizationRequest
{
    /// <summary>
    /// Submit for review instead of publishing outright. For a deployment that moderates
    /// organizations before they appear; the shipped default does not.
    /// </summary>
    public bool SubmitForReview { get; init; }
}
