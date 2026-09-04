using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Infrastructure.Hosting;

/// <summary>
/// Puts a founding set of communities into the directory at startup.
/// <para>
/// <b>The directory has to have something in it, and there is no route that creates one.</b>
/// Community creation is a later increment and a governed act — who may propose a community, who
/// approves it, what happens to a name somebody squatted. None of that is built, and a directory
/// that is empty until it is built is a screen nobody can evaluate.
/// </para>
/// <para>
/// <b>These are also what launch actually looks like.</b> A diaspora platform does not open with an
/// empty directory and wait for somebody to create "Nigerians in London" — the large national and
/// regional communities are curated, and members join them. What creation adds later is the long
/// tail, not the head.
/// </para>
/// <para>
/// <b>Every row is seeded once and never restated.</b> A community whose name, tagline or icon was
/// changed by the people running it means it, and a seeder that reasserted these values on every
/// boot would undo that silently once a night. Existence is checked by handle, which is the one
/// thing about a community that never changes.
/// </para>
/// <para>
/// <b>Failures are swallowed and logged</b>, for the reason every seeder on this platform does:
/// a directory that did not seed leaves a screen somebody notices, and an exception out of a hosted
/// service takes the host down at startup for every other module too.
/// </para>
/// </summary>
public sealed class CommunityDirectorySeeder : BackgroundService, IReferenceDataSeeder
{
    /// <summary>
    /// The founding set.
    /// <para>
    /// Chosen to exercise the model rather than to be complete: national communities with a country
    /// of origin, a regional one that spans several and has none, an interest community with no
    /// geography at all, and a city chapter that has both an origin and a presence. A directory of
    /// nothing but national diasporas would let "one country, one community" back in through the
    /// data — which is the assumption §82.2 exists to remove.
    /// </para>
    /// <para>
    /// Icons matter here: a community with no country has no flag, so the icon is the only thing
    /// between it and grey text on a card.
    /// </para>
    /// </summary>
    private static readonly CommunitySeed[] Founding =
    [
        new("nigerian-diaspora", "Nigerian Diaspora", CommunityType.NationalDiaspora,
            "Nigerians building lives around the world.", "NG", null,
            ["GB", "US", "CA", "GH", "ZA", "AE"], "globe-outline", "#008751"),

        new("ghanaian-diaspora", "Ghanaian Diaspora", CommunityType.NationalDiaspora,
            "Ghanaians at home and abroad.", "GH", null,
            ["GB", "US", "CA", "NG", "DE"], "globe-outline", "#006b3f"),

        new("kenyan-diaspora", "Kenyan Diaspora", CommunityType.NationalDiaspora,
            "Kenyans wherever they have settled.", "KE", null,
            ["GB", "US", "AE", "ZA"], "globe-outline", "#bb0000"),

        new("indian-diaspora", "Indian Diaspora", CommunityType.NationalDiaspora,
            "One of the largest diasporas in the world.", "IN", null,
            ["GB", "US", "AE", "CA", "AU", "SG"], "globe-outline", "#ff9933"),

        new("filipino-diaspora", "Filipino Diaspora", CommunityType.NationalDiaspora,
            "Filipinos working and living overseas.", "PH", null,
            ["US", "AE", "SA", "CA", "GB", "SG"], "globe-outline", "#0038a8"),

        // No single country of origin. The row that proves the directory is not a country list.
        new("west-african-community", "West African Community", CommunityType.RegionalDiaspora,
            "Fifteen countries, one region, shared history.", null, "West Africa",
            ["GB", "US", "FR", "CA"], "map-outline", "#c8102e"),

        new("caribbean-community", "Caribbean Community", CommunityType.RegionalDiaspora,
            "Islands and mainland, across the Atlantic and beyond.", null, "the Caribbean",
            ["GB", "US", "CA", "NL"], "map-outline", "#00a1de"),

        // No geography at all.
        new("diaspora-founders", "Diaspora Founders", CommunityType.InterestCommunity,
            "People building businesses away from where they grew up.", null, null,
            [], "rocket-outline", "#5b3fd6"),

        new("diaspora-healthcare-workers", "Healthcare Workers",
            CommunityType.InterestCommunity,
            "Nurses, doctors and carers who moved for the work.", null, null,
            [], "medkit-outline", "#0f7b6c"),

        // An origin and a presence, which are different fields answering different questions.
        new("nigerians-in-london", "Nigerians in London", CommunityType.CityChapter,
            "The London chapter.", "NG", null, ["GB"], "location-outline", "#008751"),

        new("ghanaians-in-toronto", "Ghanaians in Toronto", CommunityType.CityChapter,
            "The Toronto chapter.", "GH", null, ["CA"], "location-outline", "#006b3f"),
    ];

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CommunityDirectorySeeder> _logger;

    public CommunityDirectorySeeder(
        IServiceScopeFactory scopes, ILogger<CommunityDirectorySeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public string Module => CommunityProgrammeModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

            // Counted rather than inferred from the seed length afterwards. Inferring is the shape
            // that reports "Seeded 11" and added none.
            var created = 0;

            foreach (var seed in Founding)
            {
                var slug = CommunitySlug.Create(seed.Slug);

                if (slug.IsFailure)
                {
                    _logger.LogError(
                        "The seeded handle {Handle} is not valid: {Reason}",
                        seed.Slug, slug.Error.Description);

                    continue;
                }

                // By handle, because it is the one thing about a community that never changes.
                if (await communities.IsSlugTakenAsync(slug.Value, cancellationToken))
                {
                    continue;
                }

                var community = CommunityAggregate.Propose(
                    CommunityId.From(guids.NewSequentialGuid()),
                    slug.Value,
                    seed.Name,
                    seed.Type,
                    CommunityVisibility.Public,
                    seed.OriginCountryCode,
                    seed.OriginRegionName);

                if (community.IsFailure)
                {
                    _logger.LogError(
                        "The seeded community {Handle} was refused: {Reason}",
                        seed.Slug, community.Error.Description);

                    continue;
                }

                var row = community.Value;

                // Opened here rather than left Pending: a seeded community nobody can join is a
                // directory that renders and does nothing.
                row.Open();
                row.Describe(seed.Tagline, null);
                row.SetPresence(seed.PresenceCountryCodes);
                row.SetIdentity(null, seed.IconName, seed.AccentColor);

                communities.Add(row);
                created++;
            }

            if (created > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Seeded {Count} communities into the directory.", created);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown during startup. Nothing to report.
        }
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the community directory failed.");
        }
#pragma warning restore CA1031
    }

    private sealed record CommunitySeed(
        string Slug,
        string Name,
        CommunityType Type,
        string Tagline,
        string? OriginCountryCode,
        string? OriginRegionName,
        string[] PresenceCountryCodes,
        string IconName,
        string AccentColor);
}
