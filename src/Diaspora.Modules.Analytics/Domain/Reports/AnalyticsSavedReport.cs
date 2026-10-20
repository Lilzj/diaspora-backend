using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Analytics.Domain.Reports;

/// <summary>
/// A member's saved report: which subject, which figures, which period and how to draw them.
/// <para>
/// FRONTEND-PHASE15-GAP-027. <b>A definition, never a result.</b> The row keeps what to ask for and
/// nothing that was answered, so running a report always reads today's figures through the same
/// dataset routes — and the same access checks — as the dashboard it was built from. A report about a
/// subject the member can no longer see simply stops answering.
/// </para>
/// <para>
/// <b>Private to its owner.</b> The client's model has community, organization and platform sharing;
/// nothing on this deployment can resolve those audiences, so only <c>private</c> is accepted.
/// </para>
/// </summary>
public sealed class AnalyticsSavedReport : AggregateRoot<AnalyticsSavedReportId>
{
    public const int NameMaximumLength = 120;
    public const int DescriptionMaximumLength = 500;
    public const int DefinitionMaximumLength = 8000;

    private AnalyticsSavedReport(
        AnalyticsSavedReportId id,
        Guid ownerUserId,
        DateTimeOffset createdAt) : base(id)
    {
        OwnerUserId = ownerUserId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    private AnalyticsSavedReport() { }

    /// <summary>The member the report belongs to. The subject of every read, never displayed.</summary>
    public Guid OwnerUserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>The client's <c>AnalyticsQuery</c>, as JSON. Normalised by the handler before it lands.</summary>
    public string DefinitionJson { get; private set; } = "{}";

    /// <summary><c>metricCards</c>, <c>line</c>, <c>bar</c>, … — the client's visualization keys.</summary>
    public string Visualization { get; private set; } = "metricCards";

    /// <summary>Always <c>private</c> on this deployment. See the type summary.</summary>
    public string Sharing { get; private set; } = "private";

    public bool IsFavorite { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int Revision { get; private set; }

    public static AnalyticsSavedReport Create(
        AnalyticsSavedReportId id,
        Guid ownerUserId,
        string name,
        string? description,
        string definitionJson,
        string visualization,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(visualization);

        return new AnalyticsSavedReport(id, ownerUserId, createdAt)
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DefinitionJson = definitionJson,
            Visualization = visualization
        };
    }

    public void Rename(string name, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Touch(at);
    }

    public void Describe(string? description, DateTimeOffset at)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Touch(at);
    }

    public void Redefine(string definitionJson, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionJson);
        DefinitionJson = definitionJson;
        Touch(at);
    }

    public void Draw(string visualization, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(visualization);
        Visualization = visualization;
        Touch(at);
    }

    public void MarkFavorite(bool isFavorite, DateTimeOffset at)
    {
        IsFavorite = isFavorite;
        Touch(at);
    }

    private void Touch(DateTimeOffset at)
    {
        UpdatedAt = at;
        Revision++;
    }
}
