namespace Diaspora.Modules.Financial.Extensibility.Settlement;

/// <summary>
/// What a provider did with an instruction — including the answer that it does not know.
/// <para>
/// <b>Three values, because a boolean could only ever express two of them and the third is where
/// money is lost.</b> The seam used to return <c>bool</c>: true meant paid, false meant refused, and
/// a timeout had to be squeezed into one of them. It was squeezed into "refused", so the platform
/// released the hold — and if the provider had in fact sent the money, the member was paid and given
/// their balance back. That is the double payment the whole payout design exists to prevent, and no
/// amount of care inside an adapter could have avoided it while the return type had two states.
/// </para>
/// </summary>
public enum SettlementDisposition
{
    /// <summary>
    /// Nobody knows. The default, so an adapter that forgets to say lands on the answer that holds
    /// the money rather than the one that gives it back twice.
    /// </summary>
    Unknown = 0,

    /// <summary>The provider took it. The money is gone and the hold becomes a payout.</summary>
    Accepted = 1,

    /// <summary>
    /// The provider declined and <b>no money moved</b>. Only this justifies releasing the hold —
    /// it is a positive statement, not the absence of a success.
    /// </summary>
    Refused = 2
}

/// <summary>
/// One provider's answer about one instruction.
/// <para>
/// <b>The factories are the API; the constructor is not.</b> Each names the claim being made, so an
/// adapter cannot report a refusal by accident the way <c>return false</c> allowed — writing
/// <see cref="Refused"/> is a deliberate statement that no money moved.
/// </para>
/// </summary>
public sealed record SettlementOutcome
{
    private SettlementOutcome() { }

    public required SettlementDisposition Disposition { get; init; }

    /// <summary>The provider's own handle for the movement, when it gave one.</summary>
    public string? ProviderReference { get; init; }

    /// <summary>A short, stable code. Stored on the withdrawal and shown to operators.</summary>
    public string? Code { get; init; }

    /// <summary>The provider's own wording. Never a credential.</summary>
    public string? Message { get; init; }

    public bool IsAccepted => Disposition == SettlementDisposition.Accepted;

    /// <summary>
    /// Whether the hold may be released. True only for a positive refusal — see
    /// <see cref="SettlementDisposition"/>.
    /// </summary>
    public bool MayReleaseHold => Disposition == SettlementDisposition.Refused;

    /// <summary>The provider took the instruction.</summary>
    public static SettlementOutcome Accepted(string? providerReference = null) =>
        new()
        {
            Disposition = SettlementDisposition.Accepted,
            ProviderReference = providerReference
        };

    /// <summary>
    /// The provider declined, and no money moved. Say this only when the provider said so — a
    /// timeout is <see cref="Unresolved"/>.
    /// </summary>
    public static SettlementOutcome Refused(string code, string? message = null) =>
        new()
        {
            Disposition = SettlementDisposition.Refused,
            Code = code,
            Message = message
        };

    /// <summary>
    /// The outcome could not be established. The hold stays, the withdrawal stays in
    /// <c>Processing</c>, and something has to go and find out.
    /// </summary>
    public static SettlementOutcome Unresolved(string code, string? message = null) =>
        new()
        {
            Disposition = SettlementDisposition.Unknown,
            Code = code,
            Message = message
        };
}
