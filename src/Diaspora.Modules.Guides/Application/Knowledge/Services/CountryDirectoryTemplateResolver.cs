using Diaspora.Modules.Guides.Domain.Directories;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Modules.Guides.Options;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Application.Knowledge.Services;

/// <summary>
/// Arranges a brand-new country directory from whichever template applies.
/// <para>
/// <b>A country-specific template wins over the default</b>, and among equals the lowest order does.
/// That is how a deployment serving one diaspora particularly well ships its own opinionated
/// arrangement for that country without imposing it on every other country in the catalogue.
/// </para>
/// <para>
/// <b>Applied once, at creation, and never re-applied.</b> The directory is a plain arrangement from
/// that moment on, so a curator who moves Student Life to the top for a university town keeps it
/// there — a template that re-asserted itself on any later write would be a curator's work being
/// undone by a deployment's default.
/// </para>
/// <para>
/// <b>Rows whose catalogue entry does not exist are skipped rather than failing the creation.</b> A
/// template naming a row a deployment chose not to seed is an ordinary configuration difference, and
/// refusing to open a country over it would make the two seams — categories and templates —
/// impossible to configure independently.
/// </para>
/// </summary>
public sealed class CountryDirectoryTemplateResolver
{
    private readonly IEnumerable<ICountryDirectoryTemplateContributor> _contributors;
    private readonly IKnowledgeCategoryRepository _categories;
    private readonly IOptions<KnowledgeOptions> _options;

    public CountryDirectoryTemplateResolver(
        IEnumerable<ICountryDirectoryTemplateContributor> contributors,
        IKnowledgeCategoryRepository categories,
        IOptions<KnowledgeOptions> options)
    {
        _contributors = contributors;
        _categories = categories;
        _options = options;
    }

    /// <summary>
    /// Applies the best-matching template, or leaves the directory empty when none applies.
    /// <para>
    /// Silent when there is nothing to apply. A deployment that turned templates off, or ships none,
    /// gets an empty directory and a curator who arranges it — which is exactly what it asked for.
    /// </para>
    /// </summary>
    public async Task ApplyAsync(
        CountryKnowledgeDirectory directory,
        UserId? changedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(directory);

        if (!_options.Value.ApplyDirectoryTemplates)
        {
            return;
        }

        var template = Resolve(directory.CountryCode);

        if (template is null || template.KnowledgeCategoryKeys.Count == 0)
        {
            return;
        }

        var requested = template.KnowledgeCategoryKeys
            .Concat(template.HiddenKeys)
            .Select(KnowledgeNormalization.Key)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var known = await _categories.GetByKeysAsync(requested, cancellationToken);

        var hidden = new HashSet<string>(
            template.HiddenKeys.Select(KnowledgeNormalization.Key).OfType<string>(),
            StringComparer.Ordinal);

        var entries = new List<(string?, string?, bool)>();

        foreach (var key in requested)
        {
            if (known.ContainsKey(key))
            {
                entries.Add((key, null, hidden.Contains(key)));
            }
        }

        directory.Arrange(entries, changedBy);
    }

    /// <summary>
    /// The template for a country: the most specific one, then the lowest order.
    /// <para>
    /// Public so the capabilities surface and the tests can ask what a new country would start from
    /// without creating one.
    /// </para>
    /// </summary>
    public CountryDirectoryTemplate? Resolve(string countryCode)
    {
        var country = KnowledgeNormalization.Country(countryCode);

        var templates = _contributors
            .SelectMany(contributor => contributor.GetTemplates())
            .ToList();

        var specific = templates
            .Where(template => country is not null
                && string.Equals(
                    KnowledgeNormalization.Country(template.CountryCode),
                    country,
                    StringComparison.Ordinal))
            .OrderBy(template => template.Order)
            .FirstOrDefault();

        return specific ?? templates
            .Where(template => string.IsNullOrWhiteSpace(template.CountryCode))
            .OrderBy(template => template.Order)
            .FirstOrDefault();
    }
}
