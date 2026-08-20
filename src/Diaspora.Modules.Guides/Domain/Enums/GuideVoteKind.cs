namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// The two answers to "was this guide helpful?". Deliberately not a star scale — a guide is
/// either the answer to the reader's question or it is not, and a five-point scale on knowledge
/// invites rating the topic rather than the writing.
/// </summary>
public enum GuideVoteKind
{
    Helpful = 0,
    NotHelpful = 1
}
