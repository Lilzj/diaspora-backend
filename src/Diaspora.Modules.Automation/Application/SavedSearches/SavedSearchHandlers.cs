using Diaspora.Application.Abstractions;
using Diaspora.Modules.Automation.Application.Mapping;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Contracts.V1.Requests;
using Diaspora.Modules.Automation.Contracts.V1.Responses;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Errors;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.SavedSearches;
using Diaspora.Modules.Automation.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Automation.Application.SavedSearches;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-009 — saved searches, one implementation shared by five subjects.
//
// Self-scoped throughout. Saved criteria are personal — a housing search states a budget and a
// neighbourhood, a job search states a career intention — so there is no read-another-member variant
// and the subject is always the token's.
// ════════════════════════════════════════════════════════════════════════════════════════════════

[Authorize(Policy = AutomationPolicies.Read)]
public sealed record ListSavedSearchesQuery : IQuery<IReadOnlyList<SavedSearchResponse>>
{
    public string? Subject { get; init; }
}

public sealed class ListSavedSearchesQueryHandler
    : IQueryHandler<ListSavedSearchesQuery, IReadOnlyList<SavedSearchResponse>>
{
    private readonly ISavedSearchRepository _searches;
    private readonly AutomationAccessService _access;

    public ListSavedSearchesQueryHandler(
        ISavedSearchRepository searches, AutomationAccessService access)
    {
        _searches = searches;
        _access = access;
    }

    public async Task<Result<IReadOnlyList<SavedSearchResponse>>> Handle(
        ListSavedSearchesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SavedSearchResponse>>(member.Error);
        }

        var rows = await _searches.ListForMemberAsync(
            member.Value, ParseSubject(query.Subject), cancellationToken);

        return Result.Success<IReadOnlyList<SavedSearchResponse>>(
            [.. rows.Select(AutomationMapper.ToResponse)]);
    }

    internal static SavedSearchSubject? ParseSubject(string? value) =>
        Enum.TryParse<SavedSearchSubject>(value, ignoreCase: true, out var parsed) ? parsed : null;
}

/// <summary>
/// Saves a search.
/// <para>
/// Idempotent on <c>(member, subject, normalised criteria)</c>. Saving the same search twice returns
/// the existing one rather than creating a duplicate that would double every alert.
/// </para>
/// </summary>
[Authorize(Policy = AutomationPolicies.SavedSearchesWrite)]
public sealed record CreateSavedSearchCommand : ICommand<SavedSearchResponse>
{
    public required CreateSavedSearchRequest Request { get; init; }
}

public sealed class CreateSavedSearchCommandHandler
    : ICommandHandler<CreateSavedSearchCommand, SavedSearchResponse>
{
    private const int MaximumPerMember = 100;

    private readonly ISavedSearchRepository _searches;
    private readonly AutomationAccessService _access;
    private readonly IClock _clock;
    private readonly IOptions<AutomationOptions> _options;

    public CreateSavedSearchCommandHandler(
        ISavedSearchRepository searches,
        AutomationAccessService access,
        IClock clock,
        IOptions<AutomationOptions> options)
    {
        _searches = searches;
        _access = access;
        _clock = clock;
        _options = options;
    }

    public async Task<Result<SavedSearchResponse>> Handle(
        CreateSavedSearchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var request = command.Request;
        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<SavedSearchResponse>(member.Error);
        }

        if (ListSavedSearchesQueryHandler.ParseSubject(request.Subject) is not { } subject)
        {
            return Result.Failure<SavedSearchResponse>(
                AutomationErrors.SavedSearchCriteriaInvalid(
                    $"'{request.Subject}' is not something this platform can watch."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result.Failure<SavedSearchResponse>(
                AutomationErrors.SavedSearchCriteriaInvalid("A saved search needs a name."));
        }

        var criteria = string.IsNullOrWhiteSpace(request.Criteria) ? "{}" : request.Criteria;

        if (criteria.Length > _options.Value.MaximumParameterLength)
        {
            return Result.Failure<SavedSearchResponse>(
                AutomationErrors.PayloadTooLarge("search criteria"));
        }

        var fingerprint = SavedSearchMatcher.Fingerprint(criteria);

        var existing = await _searches.FindByFingerprintAsync(
            member.Value, subject, fingerprint, cancellationToken);

        if (existing is not null)
        {
            return Result.Success(AutomationMapper.ToResponse(existing));
        }

        var mine = await _searches.ListForMemberAsync(member.Value, null, cancellationToken);

        if (mine.Count >= MaximumPerMember)
        {
            return Result.Failure<SavedSearchResponse>(
                AutomationErrors.InvalidSchedule(
                    $"You already have {MaximumPerMember} saved searches, which is the most this "
                    + "platform keeps. Delete one to save another."));
        }

        var search = SavedSearch.Create(
            member.Value,
            subject,
            request.Name,
            criteria,
            fingerprint,
            request.QuerySummary ?? request.Name,
            request.AlertsEnabled,
            ParseFrequency(request.AlertFrequency),
            request.WatchFor ?? ["newMatch"],
            _clock.UtcNow);

        _searches.Add(search);
        await _searches.SaveChangesAsync(cancellationToken);

        return Result.Success(AutomationMapper.ToResponse(search));
    }

    internal static SavedSearchAlertFrequency ParseFrequency(string? value) =>
        Enum.TryParse<SavedSearchAlertFrequency>(value, ignoreCase: true, out var parsed)
            ? parsed
            : SavedSearchAlertFrequency.Instant;
}

/// <summary>Changes a saved search's name or its alert settings. Never its criteria.</summary>
[Authorize(Policy = AutomationPolicies.SavedSearchesWrite)]
public sealed record UpdateSavedSearchCommand : ICommand<SavedSearchResponse>
{
    public required Guid SavedSearchId { get; init; }

    public required UpdateSavedSearchRequest Request { get; init; }
}

public sealed class UpdateSavedSearchCommandHandler
    : ICommandHandler<UpdateSavedSearchCommand, SavedSearchResponse>
{
    private readonly ISavedSearchRepository _searches;
    private readonly AutomationAccessService _access;
    private readonly IClock _clock;

    public UpdateSavedSearchCommandHandler(
        ISavedSearchRepository searches, AutomationAccessService access, IClock clock)
    {
        _searches = searches;
        _access = access;
        _clock = clock;
    }

    public async Task<Result<SavedSearchResponse>> Handle(
        UpdateSavedSearchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<SavedSearchResponse>(member.Error);
        }

        var search = await _searches.GetAsync(
            SavedSearchId.From(command.SavedSearchId), cancellationToken);

        if (search is null || search.MemberId != member.Value)
        {
            return Result.Failure<SavedSearchResponse>(AutomationErrors.SavedSearchNotFound());
        }

        // The criteria are deliberately not editable. Changing them would change the fingerprint the
        // match history is keyed on, and every item the member had already been told about would
        // become a new match.
        search.UpdateAlerting(
            command.Request.Name,
            command.Request.AlertsEnabled,
            command.Request.AlertFrequency is null
                ? null
                : CreateSavedSearchCommandHandler.ParseFrequency(command.Request.AlertFrequency),
            command.Request.WatchFor,
            _clock.UtcNow);

        await _searches.SaveChangesAsync(cancellationToken);

        return Result.Success(AutomationMapper.ToResponse(search));
    }
}

/// <summary>Deletes a saved search. Idempotent.</summary>
[Authorize(Policy = AutomationPolicies.SavedSearchesWrite)]
public sealed record DeleteSavedSearchCommand : ICommand<SavedSearchDeletedResponse>
{
    public required Guid SavedSearchId { get; init; }
}

public sealed class DeleteSavedSearchCommandHandler
    : ICommandHandler<DeleteSavedSearchCommand, SavedSearchDeletedResponse>
{
    private readonly ISavedSearchRepository _searches;
    private readonly AutomationAccessService _access;

    public DeleteSavedSearchCommandHandler(
        ISavedSearchRepository searches, AutomationAccessService access)
    {
        _searches = searches;
        _access = access;
    }

    public async Task<Result<SavedSearchDeletedResponse>> Handle(
        DeleteSavedSearchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = _access.RequireMember();

        if (member.IsFailure)
        {
            return Result.Failure<SavedSearchDeletedResponse>(member.Error);
        }

        var search = await _searches.GetAsync(
            SavedSearchId.From(command.SavedSearchId), cancellationToken);

        if (search is not null && search.MemberId == member.Value)
        {
            _searches.Remove(search);
            await _searches.SaveChangesAsync(cancellationToken);
        }

        // The idempotent answer, and it is also the safe one: a search that is not this member's
        // answers exactly as one that never existed, so a probe learns nothing.
        return Result.Success(new SavedSearchDeletedResponse
        {
            Id = command.SavedSearchId.ToString("D"),
            Deleted = true
        });
    }
}
