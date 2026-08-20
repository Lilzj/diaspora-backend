using System.Collections;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Platform.Validation.Validators;

/// <summary>
/// Duplicate keys across every module's reference data, checked by the kernel without the kernel
/// referencing a single module.
/// <para>
/// <b>The brief asks the platform to validate notification categories, notification templates,
/// recommendation strategies and recommendation policies at startup, and the platform is not
/// allowed to know that any of those exist.</b> B13 and B14 are downstream of the kernel by
/// design — that is the whole modular-monolith claim — so a validator naming
/// <c>INotificationTemplateContributor</c> would invert the dependency the architecture suite
/// spends twenty tests protecting.
/// </para>
/// <para>
/// <b>So it validates the shape instead of the subject.</b> Every one of those catalogues is
/// declared through an <c>I…Contributor</c> seam whose declaration method returns a sequence of
/// records each carrying a <c>Key</c>, a <c>Name</c> or a <c>Code</c>. That is enough to answer the
/// question that matters — has one module quietly redefined another's row? — for every catalogue
/// on the platform at once, including the ones the next vertical brings with it.
/// </para>
/// <para>
/// <b>Across contributors, never within one, and that boundary was learned the expensive way.</b>
/// The first version of this validator reported eleven errors on a correct platform: B14's ranking
/// catalogue declares <c>fallback</c> fifteen times because a strategy is keyed by
/// <em>(key, surface)</em>, and B10's listing attributes repeat <c>colour</c> per category. A
/// composite key is invisible from out here and always will be — one identity property is all a
/// convention can see. What a convention <em>can</em> see is two different contributors claiming
/// one name, which is the collision that has actually cost this platform something: a vertical
/// landing beside the provisional catalogue it was meant to replace duplicated a feature gate and
/// stopped every member on the platform registering.
/// </para>
/// <para>
/// And it is a warning, not an error, for the same reason. Two modules contributing rows under one
/// key can be a legitimate merge — country metadata assembled from several sources is exactly
/// that — so this reports an ambiguity worth looking at rather than a state worth refusing to boot
/// over. The catalogues where a duplicate <em>is</em> fatal are the kernel's own, and those fail
/// hard in <c>SV3001</c> and <c>SV4001</c>.
/// </para>
/// <para>
/// <b>This is the one validator that runs module code, and it is written like it.</b> Every call is
/// wrapped, a contributor that throws is skipped with a warning rather than taking the host down,
/// the enumeration is capped, and the whole validator can be switched off. A contributor that reads
/// a table it has not migrated yet fails here on a cold start, which is expected and must not be
/// fatal — <c>PlatformValidationOptions.ValidateModuleCatalogs</c> exists for the deployment that
/// decides even the warning is too much.
/// </para>
/// </summary>
public sealed class ModuleCatalogValidator : IStartupValidator
{
    /// <summary>
    /// A ceiling on how much of a catalogue is read. A contributor backed by a table could return
    /// a hundred thousand rows, and this validator is looking for a naming mistake, not auditing
    /// data. Every catalogue the platform ships is two orders of magnitude below it.
    /// </summary>
    private const int MaximumEntries = 2_000;

    private static readonly string[] IdentityProperties = ["Key", "Name", "Code"];

    public string Name => nameof(ModuleCatalogValidator);

    public IEnumerable<StartupFinding> Validate(StartupValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.Options.ValidateModuleCatalogs)
        {
            yield break;
        }

        foreach (var seam in ContributorSeams.Find(context.Assemblies))
        {
            if (!context.IsRegistered(seam))
            {
                continue;
            }

            // Keyed by the declaration method, not by the seam: a contributor with two methods —
            // categories and templates, say — declares two catalogues, and a key colliding across
            // them is not a collision at all.
            var declared = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

            foreach (var contributor in Resolve(context, seam))
            {
                Collect(contributor, declared);
            }

            foreach (var finding in Duplicates(seam, declared))
            {
                yield return finding;
            }
        }
    }

    private static List<object> Resolve(StartupValidationContext context, Type seam)
    {
        try
        {
            return context.Services.GetServices(seam).OfType<object>().ToList();
        }
#pragma warning disable CA1031 // Resolution is the ContributorRegistrationValidator's finding, not this one's.
        catch (Exception)
#pragma warning restore CA1031
        {
            return [];
        }
    }

    private static void Collect(
        object contributor, Dictionary<string, Dictionary<string, List<string>>> declared)
    {
        foreach (var method in DeclarationMethods(contributor.GetType()))
        {
            IEnumerable? entries;

            try
            {
                entries = method.Invoke(contributor, null) as IEnumerable;
            }
#pragma warning disable CA1031 // A contributor that throws is skipped; see the class summary.
            catch (Exception)
#pragma warning restore CA1031
            {
                continue;
            }

            if (entries is null)
            {
                continue;
            }

            var catalogue = declared.TryGetValue(method.Name, out var existing)
                ? existing
                : declared[method.Name] = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            var read = 0;

            foreach (var entry in entries)
            {
                if (entry is null || ++read > MaximumEntries)
                {
                    break;
                }

                if (Identity(entry) is not { } identity)
                {
                    continue;
                }

                if (!catalogue.TryGetValue(identity, out var owners))
                {
                    catalogue[identity] = owners = [];
                }

                // Distinct owners only. One contributor repeating a key is a composite key this
                // rule cannot see — see the class summary — and counting it would report every
                // correctly-modelled catalogue on the platform.
                var owner = Describe(contributor);

                if (!owners.Contains(owner))
                {
                    owners.Add(owner);
                }
            }
        }
    }

    private static IEnumerable<StartupFinding> Duplicates(
        Type seam, Dictionary<string, Dictionary<string, List<string>>> declared)
    {
        foreach (var (method, catalogue) in declared)
        {
            foreach (var (identity, owners) in catalogue.Where(entry => entry.Value.Count > 1))
            {
                yield return StartupFinding.Warning(
                    "SV6001",
                    $"{seam.Name}.{method} → '{identity}'",
                    $"is declared by {string.Join(" and ", owners.Order(StringComparer.Ordinal))}. " +
                    "Reference data composed from two contributors' declarations of one key is " +
                    "whichever the seeder reached last, and the one that lost does not know it lost.");
            }
        }
    }

    /// <summary>
    /// How a contributor is named in a finding: its module when it declares one, and its type when
    /// it does not — with the type always present, because two contributors from one module are
    /// otherwise indistinguishable in the message that names them both.
    /// </summary>
    private static string Describe(object contributor) =>
        ContributorSeams.DeclaredModule(contributor) is { Length: > 0 } module
            ? $"{contributor.GetType().Name} ({module})"
            : contributor.GetType().Name;

    /// <summary>
    /// A contributor's declaration methods: public, no parameters, returning a sequence of
    /// something that is not a string. That shape is what every reference-data seam on the platform
    /// uses, and nothing else on a contributor matches it.
    /// </summary>
    private static IEnumerable<MethodInfo> DeclarationMethods(Type contributor) =>
        contributor.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method =>
                method.GetParameters().Length == 0 &&
                !method.IsSpecialGenericOrObjectMember() &&
                method.ReturnType != typeof(string) &&
                typeof(IEnumerable).IsAssignableFrom(method.ReturnType) &&
                method.ReturnType != typeof(IEnumerable<string>));

    /// <summary>
    /// The entry's own name, by convention. Returns null when nothing on it looks like an
    /// identifier, which means the catalogue is simply not one this rule can check — silently, and
    /// correctly.
    /// </summary>
    private static string? Identity(object entry)
    {
        foreach (var name in IdentityProperties)
        {
            var property = entry.GetType().GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);

            if (property?.PropertyType != typeof(string))
            {
                continue;
            }

            if (property.GetValue(entry) is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

internal static class MethodInfoExtensions
{
    /// <summary>
    /// Excludes property getters, generic definitions and everything inherited from
    /// <see cref="object"/>. Without this the sweep would invoke <c>GetType</c> on every
    /// contributor in the platform and try to read a key off a <see cref="Type"/>.
    /// </summary>
    public static bool IsSpecialGenericOrObjectMember(this MethodInfo method) =>
        method.IsSpecialName ||
        method.IsGenericMethodDefinition ||
        method.DeclaringType == typeof(object);
}
