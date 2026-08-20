using Diaspora.Modules.Guides.Application.Services.Snapshots;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Compares two deserialized snapshots at section granularity.
/// <para>
/// Sections are matched by their stable identifiers, never by position or title — the editor
/// keeps a section's id across saves, so a retitle and a replacement are different facts and
/// this diff can tell them apart. Block comparison is structural: two sections differ when
/// any block's identity, kind, content, media reference or payload differs, in order.
/// </para>
/// <para>
/// Static, because a diff is a pure function of its inputs — the same two versions produce
/// the same answer forever, which is what makes the endpoint cacheable.
/// </para>
/// </summary>
public static class GuideVersionDiffService
{
    /// <summary>The wire names for what happened to a section.</summary>
    private const string Added = "added";
    private const string Removed = "removed";
    private const string Retitled = "retitled";
    private const string ContentChanged = "content-changed";

    /// <summary>
    /// Everything that differs between <paramref name="from"/> and <paramref name="to"/>.
    /// Unchanged sections are omitted — the diff exists to show what moved.
    /// </summary>
    public static GuideVersionDiffResponse Diff(
        Guid guideId,
        int fromVersion,
        GuideSnapshot from,
        int toVersion,
        GuideSnapshot to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var older = from.Sections.ToDictionary(section => section.SectionId);
        var newer = to.Sections.ToDictionary(section => section.SectionId);

        var sections = new List<GuideSectionDiffResponse>();

        // Additions and changes, in the target version's reading order.
        foreach (var section in to.Sections.OrderBy(section => section.Order))
        {
            if (!older.TryGetValue(section.SectionId, out var previous))
            {
                sections.Add(new GuideSectionDiffResponse
                {
                    SectionId = section.SectionId,
                    Change = Added,
                    ToTitle = section.Title,
                    ContentChanged = true
                });

                continue;
            }

            var retitled = !string.Equals(previous.Title, section.Title, StringComparison.Ordinal);
            var contentChanged = !SameBlocks(previous, section);

            if (!retitled && !contentChanged)
            {
                continue;
            }

            sections.Add(new GuideSectionDiffResponse
            {
                SectionId = section.SectionId,
                Change = contentChanged ? ContentChanged : Retitled,
                FromTitle = previous.Title,
                ToTitle = section.Title,
                ContentChanged = contentChanged
            });
        }

        // Removals, in the older version's reading order, after everything that survived.
        foreach (var section in from.Sections.OrderBy(section => section.Order))
        {
            if (!newer.ContainsKey(section.SectionId))
            {
                sections.Add(new GuideSectionDiffResponse
                {
                    SectionId = section.SectionId,
                    Change = Removed,
                    FromTitle = section.Title,
                    ContentChanged = true
                });
            }
        }

        return new GuideVersionDiffResponse
        {
            GuideId = guideId,
            FromVersion = fromVersion,
            ToVersion = toVersion,
            Sections = sections
        };
    }

    /// <summary>Whether two sections carry the same blocks, in the same order.</summary>
    private static bool SameBlocks(GuideSectionSnapshot older, GuideSectionSnapshot newer)
    {
        var left = older.Blocks.OrderBy(block => block.Order).ToList();
        var right = newer.Blocks.OrderBy(block => block.Order).ToList();

        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!SameBlock(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameBlock(GuideBlockSnapshot left, GuideBlockSnapshot right) =>
        left.BlockId == right.BlockId
        && string.Equals(left.Kind, right.Kind, StringComparison.Ordinal)
        && string.Equals(left.Content, right.Content, StringComparison.Ordinal)
        && left.MediaId == right.MediaId
        && string.Equals(left.Metadata, right.Metadata, StringComparison.Ordinal);
}
