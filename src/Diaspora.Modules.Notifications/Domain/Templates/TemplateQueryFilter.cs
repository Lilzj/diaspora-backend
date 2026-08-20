using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Domain.Templates;

/// <summary>How the template catalogue is sliced on the authoring screen.</summary>
public sealed record TemplateQueryFilter
{
    public string? Search { get; init; }

    public string? CategoryKey { get; init; }

    public TemplateStatus? Status { get; init; }

    public string? Language { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
