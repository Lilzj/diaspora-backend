namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Withdraw a response.</summary>
public sealed record WithdrawRsvpRequest
{
    public string? Reason { get; init; }
}
