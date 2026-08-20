namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Approve or refuse a membership request.</summary>
public sealed record DecideMembershipRequest
{
    public required bool Approve { get; init; }

    /// <summary>Why, on a refusal. Shown to the applicant.</summary>
    public string? Reason { get; init; }
}
