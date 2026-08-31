using System.Globalization;

using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// Replaces a provider's opening hours for the days the request names.
/// <para>
/// Requires <see cref="ProviderRole.Editor"/>. Marked <c>[Transactional]</c>, which is what makes
/// a week atomic: the domain's <c>SetDayHours</c> is per-day and is called once per entry, so
/// without a single transaction a client saving Monday–Sunday could half-succeed and leave a
/// business advertising three days of a schedule it never agreed to.
/// </para>
/// <para>
/// <b>Every entry is validated before any is applied.</b> Parsing the whole payload first means
/// a typo in Friday's closing time cannot leave Monday through Thursday already written — even
/// inside a transaction, a partially-mutated aggregate is a trap for anything that inspects it
/// between the failure and the rollback.
/// </para>
/// </summary>
public sealed class SetProviderOperatingHoursCommandHandler
    : ICommandHandler<SetProviderOperatingHoursCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IGuidGenerator _guids;

    public SetProviderOperatingHoursCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper,
        IGuidGenerator guids)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
        _guids = guids;
    }

    /// <summary>One day's schedule, with everything already parsed into domain types.</summary>
    private sealed record ResolvedDay(
        DayOfWeek Day,
        bool IsClosed,
        IReadOnlyList<(AvailabilityWindowId Id, TimeOnly OpensAt, TimeOnly ClosesAt, string? Label)> Windows);

    public async Task<Result<ProviderResponse>> Handle(
        SetProviderOperatingHoursCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = await _providers.GetAsync(
            ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var caller = _authorization.RequireRole(provider, ProviderRole.Editor);

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        var resolved = Resolve(command.Request);

        if (resolved.IsFailure)
        {
            return Result.Failure<ProviderResponse>(resolved.Error);
        }

        foreach (var day in resolved.Value)
        {
            var applied = provider.SetDayHours(
                day.Day,
                day.IsClosed,
                day.Windows,
                OperatingHoursId.From(_guids.NewGuid()),
                caller.Value);

            if (applied.IsFailure)
            {
                return Result.Failure<ProviderResponse>(applied.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }

    /// <summary>Parses the whole payload, refusing on the first thing that is not readable.</summary>
    private Result<IReadOnlyList<ResolvedDay>> Resolve(SetProviderOperatingHoursRequest request)
    {
        var days = new List<ResolvedDay>(request.Hours.Count);
        var seen = new HashSet<DayOfWeek>();

        foreach (var entry in request.Hours)
        {
            var day = ProviderWireNames.ParseDayOfWeek(entry.DayOfWeek);

            if (day.IsFailure)
            {
                return Result.Failure<IReadOnlyList<ResolvedDay>>(day.Error);
            }

            if (!seen.Add(day.Value))
            {
                return Result.Failure<IReadOnlyList<ResolvedDay>>(
                    ProviderErrors.OperatingHoursDayDuplicated);
            }

            // A closed day carries no windows, and any it happens to carry are discarded rather
            // than stored: `IsClosed` is the statement, and keeping unreachable windows behind it
            // would resurface them the moment somebody reopened the day.
            if (entry.IsClosed)
            {
                days.Add(new ResolvedDay(day.Value, IsClosed: true, []));
                continue;
            }

            var windows = new List<(AvailabilityWindowId, TimeOnly, TimeOnly, string?)>(entry.Windows.Count);

            foreach (var window in entry.Windows)
            {
                var opensAt = ParseTime(window.OpensAt);
                var closesAt = ParseTime(window.ClosesAt);

                if (opensAt is null || closesAt is null)
                {
                    return Result.Failure<IReadOnlyList<ResolvedDay>>(
                        ProviderErrors.OperatingHoursTimeInvalid);
                }

                windows.Add((
                    AvailabilityWindowId.From(_guids.NewGuid()),
                    opensAt.Value,
                    closesAt.Value,
                    window.Label));
            }

            days.Add(new ResolvedDay(day.Value, IsClosed: false, windows));
        }

        return Result.Success<IReadOnlyList<ResolvedDay>>(days);
    }

    /// <summary>
    /// <c>HH:mm</c> only, in the invariant culture.
    /// <para>
    /// Deliberately not <c>TimeOnly.Parse</c>: that accepts whatever the ambient culture allows,
    /// so the same payload would be read differently on two hosts, and on some of them "10:30"
    /// with an AM/PM culture is ambiguous. <c>HH:mm</c> is what
    /// <see cref="AvailabilityWindowEntry"/> documents, and it is all that is accepted.
    /// </para>
    /// <para>
    /// A closing time numerically before its opening time is <b>not</b> rejected here — that is
    /// how a bar open 20:00–02:00 is expressed, and the domain owns that reading.
    /// </para>
    /// </summary>
    private static TimeOnly? ParseTime(string? value) =>
        TimeOnly.TryParseExact(
            value?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
