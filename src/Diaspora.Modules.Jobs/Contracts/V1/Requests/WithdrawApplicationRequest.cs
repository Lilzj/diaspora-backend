namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// The candidate withdraws.
/// <para>
/// The reason is always optional. Requiring one to leave a hiring process would be a small
/// cruelty, and an unwilling answer is worthless data anyway.
/// </para>
/// </summary>
public sealed record WithdrawApplicationRequest
{
    public string? Reason { get; init; }
}
