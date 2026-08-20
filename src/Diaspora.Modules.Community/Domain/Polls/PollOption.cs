using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Polls;

/// <summary>
/// One answer a member can pick.
/// <para>
/// Carries its own vote count, denormalised for the same reason every other counter in this
/// module is: rendering a poll means rendering every option's share, and computing that from
/// the vote table means one aggregate query per option on every read of every card.
/// </para>
/// <para>
/// An option may carry an image, and it is a media reference like everything else — a poll
/// asking which of four flats looks better is a real use of this, and it must not become the
/// one place in the module that stores a URL.
/// </para>
/// </summary>
public sealed class PollOption : Entity<PollOptionId>
{
    public const int MaxLabelLength = 200;

    private PollOption(
        PollOptionId id, PollId pollId, string label, int position, MediaId? imageMediaId)
        : base(id)
    {
        PollId = pollId;
        Label = label;
        Position = position;
        ImageMediaId = imageMediaId;
    }

    private PollOption() { }

    public PollId PollId { get; private set; } = default!;

    public string Label { get; private set; } = string.Empty;

    public int Position { get; private set; }

    /// <summary>A Media reference. Never a URL.</summary>
    public MediaId? ImageMediaId { get; private set; }

    public int VoteCount { get; private set; }

    internal static PollOption Create(
        PollId pollId, string label, int position, MediaId? imageMediaId)
    {
        var trimmed = (label ?? string.Empty).Trim();

        return new PollOption(
            PollOptionId.New(),
            pollId,
            trimmed.Length > MaxLabelLength ? trimmed[..MaxLabelLength] : trimmed,
            position,
            imageMediaId);
    }

    internal void AdjustVotes(int delta) => VoteCount = Math.Max(0, VoteCount + delta);
}
