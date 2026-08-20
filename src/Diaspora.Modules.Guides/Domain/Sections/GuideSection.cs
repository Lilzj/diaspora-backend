using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Sections;

/// <summary>
/// One titled chapter of a guide's current draft.
/// <para>
/// Owned by the guide and rebuilt wholesale on every editor save, which is why creation is
/// internal: a section constructed anywhere except <c>Guide.ReplaceStructure</c> would be a
/// section whose count, ordering and block invariants nobody checked against its siblings.
/// The identifier is a plain <see cref="Guid"/> supplied by the editor and stable across
/// saves — it is what version diffs match sections by, so "renamed" and "replaced" stay
/// distinguishable.
/// </para>
/// </summary>
public sealed class GuideSection : Entity<Guid>
{
    private readonly List<GuideBlock> _blocks = [];

    private GuideSection(Guid id, string title, int order)
        : base(id)
    {
        Title = title;
        Order = order;
    }

    private GuideSection() { }

    public string Title { get; private set; } = string.Empty;

    public int Order { get; private set; }

    public IReadOnlyList<GuideBlock> Blocks =>
        _blocks.OrderBy(block => block.Order).ToList().AsReadOnly();

    /// <summary>Whether at least one block has something a reader would see.</summary>
    public bool HasContent => _blocks.Exists(block => block.HasContent);

    /// <summary>The words a reader reads here. Feeds the published reading time.</summary>
    public int WordCount => _blocks.Sum(block => block.WordCount);

    internal static Result<GuideSection> Create(GuideSectionDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (draft.SectionId == Guid.Empty)
        {
            return Result.Failure<GuideSection>(GuideErrors.SectionInvalid);
        }

        var title = draft.Title?.Trim();

        if (string.IsNullOrEmpty(title) || title.Length > GuideLimits.MaxSectionTitle)
        {
            return Result.Failure<GuideSection>(GuideErrors.SectionInvalid);
        }

        if (draft.Blocks.Count > GuideLimits.MaxBlocksPerSection)
        {
            return Result.Failure<GuideSection>(GuideErrors.BlockLimit);
        }

        var section = new GuideSection(draft.SectionId, title, draft.Order);

        foreach (var blockDraft in draft.Blocks.OrderBy(block => block.Order))
        {
            var block = GuideBlock.Create(blockDraft);

            if (block.IsFailure)
            {
                return Result.Failure<GuideSection>(block.Error);
            }

            section._blocks.Add(block.Value);
        }

        return Result.Success(section);
    }
}
