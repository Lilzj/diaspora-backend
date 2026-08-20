namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Opens a knowledge directory for a country.
/// <para>
/// The country code comes from the route, not the body — it is the resource's identity, and a body
/// that could disagree with the URL is a body somebody will eventually make disagree.
/// </para>
/// <para>
/// Created unpublished, and arranged from a template if the deployment has one. Publishing is a
/// separate, deliberate act.
/// </para>
/// </summary>
public sealed record CreateCountryDirectoryRequest
{
    /// <summary>The country's name as this platform writes it. Defaults to the country code.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The standfirst under the name. Editorial copy, rendered verbatim.</summary>
    public string? Summary { get; init; }

    /// <summary>The language most guides here are written in. A hint, never a filter.</summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>The other languages this country's knowledge is written in.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];
}
