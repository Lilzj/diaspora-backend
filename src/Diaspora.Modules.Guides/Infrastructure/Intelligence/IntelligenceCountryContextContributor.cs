using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Guides.Infrastructure.Intelligence;

/// <summary>
/// Puts "12 official notices" and "3 missions" on a country dashboard's context row.
/// <para>
/// <b>This is B8.2 using B8.1's seam as designed, and it is deliberately <em>not</em> how the
/// intelligence band reaches the dashboard.</b> The band carries content — the emergency numbers are
/// rendered, not counted — and goes through a direct dependency because it lives in the same
/// assembly. This contributor is the other half: two numbers and two routes, sitting alongside
/// "142 jobs" and "38 homes" in the row a member scans to see what a country holds.
/// </para>
/// <para>
/// <b>Counted live rather than projected.</b> Every other block in that row comes from a counter
/// maintained out of band from published contracts, because those verticals are modules this one
/// cannot reference. This one owns its data, so it counts it — which is exact rather than eventually
/// consistent, and costs two indexed counts on a page that is cached anyway.
/// </para>
/// <para>
/// <b>A zero-count block is returned here and dropped by the registry</b>, which is B8.1's rule for
/// every contributor and is the right one for a row of counters: a country page listing
/// "0 official notices" beside "142 jobs" reads as a broken feature rather than a new country. This
/// contributor does not filter, because whether an empty band is worth rendering is a decision about
/// the row as a whole and belongs where the row is assembled.
/// </para>
/// </summary>
public sealed class IntelligenceCountryContextContributor : ICountryContextContributor
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IEmbassyRepository _embassies;
    private readonly IClock _clock;

    public IntelligenceCountryContextContributor(
        IOfficialNoticeRepository notices, IEmbassyRepository embassies, IClock clock)
    {
        _notices = notices;
        _embassies = embassies;
        _clock = clock;
    }

    public string Module => GuidesModuleMetadata.Name;

    /// <summary>
    /// Before the projected counters, because official information outranks inventory on a page
    /// somebody may be reading to find out whether it is safe to travel.
    /// </summary>
    public int Order => 50;

    public async Task<IReadOnlyList<CountryContextBlock>> GetBlocksAsync(
        string countryCode, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var notices = await _notices.CountAsync(
            new NoticeQueryFilter
            {
                CountryCode = countryCode,
                ActiveOnly = true,
                AsOf = now,
                Sort = NoticeSortOrder.Priority
            },
            cancellationToken);

        var embassies = await _embassies.CountAsync(
            countryCode, serving: null, cancellationToken);

        return
        [
            new CountryContextBlock
            {
                Kind = "official-notices",
                Label = "Official notices",
                Count = notices,
                Route =
                    $"{GuidesModuleMetadata.IntelligenceRoutePrefix}/notices" +
                    $"?countryCode={countryCode}&activeOnly=true",
                Order = 10
            },
            new CountryContextBlock
            {
                Kind = "embassies",
                Label = "Embassies and consulates",
                Count = embassies,
                Route =
                    $"{GuidesModuleMetadata.IntelligenceRoutePrefix}/embassies" +
                    $"?locatedIn={countryCode}",
                Order = 20
            }
        ];
    }
}
