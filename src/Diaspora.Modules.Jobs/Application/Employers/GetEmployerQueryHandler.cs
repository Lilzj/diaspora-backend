using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Application.Mapping;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Employers;

/// <summary>
/// Reads one employer profile, projected for whoever is asking.
/// </summary>
public sealed class GetEmployerQueryHandler : IQueryHandler<GetEmployerQuery, EmployerResponse>
{
    private readonly IEmployerProfileRepository _employers;
    private readonly IJobPostingRepository _postings;
    private readonly JobAuthorizationService _authorization;

    public GetEmployerQueryHandler(
        IEmployerProfileRepository employers,
        IJobPostingRepository postings,
        JobAuthorizationService authorization)
    {
        _employers = employers;
        _postings = postings;
        _authorization = authorization;
    }

    public async Task<Result<EmployerResponse>> Handle(
        GetEmployerQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var employer = await ResolveAsync(query.IdOrSlug, cancellationToken);

        if (employer is null)
        {
            return Result.Failure<EmployerResponse>(JobErrors.EmployerNotFound);
        }

        // The viewer's role decides the projection, and a null role is the ordinary case rather
        // than a refusal: an employer profile is the thing a job posting links to, so a reader who
        // is not on the team must be able to open it. `EmployerMapper` withholds the registry
        // number, the legal name and the member list from them.
        //
        // Read off the aggregate rather than through a second query — `RoleOf` is the same
        // membership check `IsMemberOfAsync` performs, and the profile is already loaded.
        var caller = _authorization.RequireCaller();
        var viewerRole = caller.IsSuccess ? employer.RoleOf(caller.Value) : null;

        var openJobs = await _postings.CountOpenByEmployerAsync(employer.Id, cancellationToken);

        return Result.Success(EmployerMapper.ToResponse(employer, openJobs, viewerRole));
    }

    /// <summary>
    /// A GUID or a slug. Tried in that order because a GUID cannot be a valid slug, so there is no
    /// ambiguity to resolve — and because a link shared into a conversation carries the slug.
    /// </summary>
    private async Task<EmployerProfile?> ResolveAsync(
        string idOrSlug, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(idOrSlug, out var id))
        {
            return await _employers.GetForReadAsync(EmployerProfileId.From(id), cancellationToken);
        }

        var slug = JobSlug.Create(idOrSlug);

        return slug.IsSuccess
            ? await _employers.GetBySlugAsync(slug.Value, cancellationToken)
            : null;
    }
}
