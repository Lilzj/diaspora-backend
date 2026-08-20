using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Turns the editor's wire structure into the drafts the aggregate takes.
/// <para>
/// One definition, because three commands submit a document — the full-document save, the
/// auto-save and the version restore — and three copies of a block-kind parse is three places
/// for the vocabulary to drift. The only judgement here is the kind lookup: an unknown kind
/// fails the whole save rather than dropping a block, because a partial document written under
/// a success code is a corruption nobody notices until the author reopens it.
/// </para>
/// </summary>
internal static class GuideStructureInputMapper
{
    public static Result<IReadOnlyList<GuideSectionDraft>> ToDrafts(
        IReadOnlyList<GuideSectionInput> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        var drafts = new List<GuideSectionDraft>(sections.Count);

        foreach (var section in sections)
        {
            if (section is null)
            {
                return Result.Failure<IReadOnlyList<GuideSectionDraft>>(GuideErrors.SectionInvalid);
            }

            var blocks = new List<GuideBlockDraft>(section.Blocks.Count);

            foreach (var block in section.Blocks)
            {
                if (block is null)
                {
                    return Result.Failure<IReadOnlyList<GuideSectionDraft>>(
                        GuideErrors.BlockInvalid);
                }

                if (GuideWireNames.ParseBlockKind(block.Kind) is not { } kind)
                {
                    return Result.Failure<IReadOnlyList<GuideSectionDraft>>(
                        GuideErrors.BlockInvalid);
                }

                blocks.Add(new GuideBlockDraft
                {
                    BlockId = block.BlockId,
                    Kind = kind,
                    Order = block.Order,
                    Content = block.Content,
                    MediaId = block.MediaId,
                    Metadata = block.Metadata
                });
            }

            drafts.Add(new GuideSectionDraft
            {
                SectionId = section.SectionId,
                Title = section.Title,
                Order = section.Order,
                Blocks = blocks
            });
        }

        return Result.Success<IReadOnlyList<GuideSectionDraft>>(drafts);
    }

    /// <summary>Every asset a document's blocks reference, deduplicated, in reading order.</summary>
    public static IReadOnlyList<Guid> MediaIds(IReadOnlyList<GuideSectionDraft> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        return sections
            .SelectMany(section => section.Blocks)
            .Select(block => block.MediaId)
            .Where(mediaId => mediaId is not null && mediaId != Guid.Empty)
            .Select(mediaId => mediaId!.Value)
            .Distinct()
            .ToList();
    }
}
