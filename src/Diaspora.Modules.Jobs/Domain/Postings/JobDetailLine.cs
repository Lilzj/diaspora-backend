using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// One bulleted line on a posting — a responsibility, a requirement or a benefit.
/// <para>
/// Which list it belongs to is <see cref="Kind"/>; see <see cref="JobDetailKind"/> for why the
/// three lists share one type. <see cref="Order"/> is per kind, so the first requirement and
/// the first benefit both sit at zero.
/// </para>
/// <para>
/// Creation is internal: lines are replaced wholesale through the aggregate, which owns the
/// count invariants.
/// </para>
/// </summary>
public sealed class JobDetailLine
{
    private JobDetailLine(Guid lineId, JobDetailKind kind, string text, int order, bool isEssential)
    {
        LineId = lineId;
        Kind = kind;
        Text = text;
        Order = order;
        IsEssential = isEssential;
    }

    private JobDetailLine() { }

    /// <summary>
    /// The line's own identity. Caller-supplied, so an edit that reorders lines updates rows
    /// rather than deleting and recreating them — which is what keeps the table's row
    /// identities stable across a whole-document save.
    /// </summary>
    public Guid LineId { get; private set; }

    public JobDetailKind Kind { get; private set; }

    public string Text { get; private set; } = string.Empty;

    /// <summary>Position within this kind's list.</summary>
    public int Order { get; private set; }

    /// <summary>
    /// For a requirement, whether it is a hard bar or a nice-to-have.
    /// <para>
    /// Worth a column rather than a convention, because "desirable" requirements are the single
    /// biggest reason qualified people do not apply — and a platform that can render the
    /// distinction can stop a candidate self-rejecting over a preference. Meaningless on the
    /// other two kinds, where it is always true.
    /// </para>
    /// </summary>
    public bool IsEssential { get; private set; }

    /// <summary>
    /// Builds a line, or null when the text is empty or too long. Null rather than a Result
    /// because the aggregate validates the whole list as a set and reports one failure for it.
    /// </summary>
    internal static JobDetailLine? Create(
        Guid lineId, JobDetailKind kind, string? text, int order, bool isEssential = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var collapsed = string.Join(' ', text.Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (collapsed.Length == 0 || collapsed.Length > JobLimits.MaxBulletLength)
        {
            return null;
        }

        return new JobDetailLine(
            lineId == Guid.Empty ? Guid.NewGuid() : lineId,
            kind,
            collapsed,
            order,
            kind == JobDetailKind.Requirement ? isEssential : true);
    }
}
