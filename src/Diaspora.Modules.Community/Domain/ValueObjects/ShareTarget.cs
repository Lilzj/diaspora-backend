using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.ValueObjects;

/// <summary>
/// Where a share went.
/// <para>
/// An external share is the one social event a platform genuinely cannot observe: the operating
/// system share sheet takes over and nothing comes back. What B6 records is therefore what the
/// client <em>says</em> happened, and this type keeps that honest by naming the destination as
/// a self-declared string rather than an enum. An enum would imply the platform verified it.
/// </para>
/// <para>
/// The destination is capped and stored as free text on purpose: share sheets gain
/// destinations constantly, and a share to an app B6 has never heard of is still worth counting.
/// </para>
/// </summary>
public sealed class ShareTarget : ValueObject
{
    public const int MaxDestinationLength = 64;

    private ShareTarget(ShareKind kind, string? destination, Guid? recipientId)
    {
        Kind = kind;
        Destination = destination;
        RecipientId = recipientId;
    }

    private ShareTarget() { }

    public ShareKind Kind { get; private set; }

    /// <summary>
    /// Self-declared, for an external share. Lower-cased so <c>WhatsApp</c> and <c>whatsapp</c>
    /// do not become two rows in a report.
    /// </summary>
    public string? Destination { get; private set; }

    /// <summary>Set for an internal share: the member or conversation it was sent to.</summary>
    public Guid? RecipientId { get; private set; }

    public static ShareTarget Internal(Guid recipientId) =>
        new(ShareKind.Internal, null, recipientId);

    public static ShareTarget External(string? destination) =>
        new(ShareKind.External, Normalise(destination), null);

    public static ShareTarget Quote() => new(ShareKind.Quote, null, null);

    public static ShareTarget Reshare() => new(ShareKind.Reshare, null, null);

    private static string? Normalise(string? destination)
    {
        var trimmed = destination?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > MaxDestinationLength
            ? trimmed[..MaxDestinationLength]
            : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return Destination;
        yield return RecipientId;
    }
}
