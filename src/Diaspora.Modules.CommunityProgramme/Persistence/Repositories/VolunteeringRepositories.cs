using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Repositories;

/// <summary>Reads and writes volunteer opportunities.</summary>
public sealed class VolunteerOpportunityRepository : IVolunteerOpportunityRepository
{
    private readonly DiasporaDbContext _dbContext;

    public VolunteerOpportunityRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<VolunteerOpportunity> Opportunities => _dbContext.Set<VolunteerOpportunity>();

    public async Task<VolunteerOpportunity?> GetAsync(
        VolunteerOpportunityId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Opportunities.FirstOrDefaultAsync(
            opportunity => opportunity.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<VolunteerOpportunityId, VolunteerOpportunity>> FindManyAsync(
        IReadOnlyCollection<VolunteerOpportunityId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var distinct = ids.Distinct().ToList();

        if (distinct.Count == 0)
        {
            return new Dictionary<VolunteerOpportunityId, VolunteerOpportunity>();
        }

        var rows = await Opportunities
            .AsNoTracking()
            .Where(opportunity => distinct.Contains(opportunity.Id))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(opportunity => opportunity.Id);
    }

    public async Task<CursorPagedResult<VolunteerOpportunity>> SearchAsync(
        OpportunityQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Opportunities.AsNoTracking();

        // A draft is visible only to somebody who manages the body that owns it. Applied in the
        // query rather than after it: a draft that reached the device was already disclosed.
        var managed = filter.ManagedOrganizationIds?.ToList() ?? [];

        query = query.Where(opportunity =>
            opportunity.Status != VolunteerOpportunityStatus.Draft
            || managed.Contains(opportunity.OrganizationId));

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(opportunity => statuses.Contains(opportunity.Status));
        }

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(opportunity => opportunity.OrganizationId == organizationId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Cause))
        {
            var cause = filter.Cause.Trim();
            query = query.Where(opportunity => opportunity.Cause == cause);
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(opportunity => opportunity.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            var city = filter.CityName.Trim();
            query = query.Where(opportunity => opportunity.CityName == city);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();

            query = query.Where(opportunity =>
                EF.Functions.Like(opportunity.Title, $"%{term}%")
                || EF.Functions.Like(opportunity.Description, $"%{term}%"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Skill))
        {
            var skill = filter.Skill.Trim();

            // Over the owned skill collection, in the database. `Any` on an owned collection
            // translates; projecting RequiredSkills — which is [NotMapped] — would not.
            query = query.Where(opportunity =>
                EF.Property<ICollection<OpportunitySkill>>(opportunity, "_requiredSkills")
                    .Any(row => row.Name == skill));
        }

        if (filter.From is { } from)
        {
            query = query.Where(opportunity =>
                opportunity.StartsAt == null || opportunity.StartsAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(opportunity =>
                opportunity.StartsAt == null || opportunity.StartsAt <= to);
        }

        var cursor = CommunityProgrammeCursor.Parse(filter.Cursor);

        query = CursorPage.Apply(
            query,
            cursor,
            VolunteerOpportunityId.From,
            opportunity => opportunity.CreatedAt,
            opportunity => opportunity.Id);

        query = query
            .OrderByDescending(opportunity => opportunity.CreatedAt)
            .ThenBy(opportunity => opportunity.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            cursor,
            Math.Clamp(filter.Limit, 1, CommunityProgrammeLimits.MaxPageSize),
            opportunity => opportunity.CreatedAt,
            opportunity => opportunity.Id.Value,
            cancellationToken);
    }

    public async Task<VolunteerOpportunity?> FindByIdempotencyKeyAsync(
        UserId createdByUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Opportunities.FirstOrDefaultAsync(
            opportunity => opportunity.CreatedByUserId == createdByUserId
                && opportunity.IdempotencyKey == normalized,
            cancellationToken);
    }

    public async Task<IReadOnlyList<VolunteerOpportunity>> FindPastDeadlineAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default)
    {
        var open = new[]
        {
            VolunteerOpportunityStatus.Published,
            VolunteerOpportunityStatus.Full
        };

        return await Opportunities
            .Where(opportunity => open.Contains(opportunity.Status)
                && opportunity.ApplicationDeadline != null
                && opportunity.ApplicationDeadline <= asOf)
            .OrderBy(opportunity => opportunity.ApplicationDeadline)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountByStatusAsync(
        Guid organizationId,
        VolunteerOpportunityStatus status,
        CancellationToken cancellationToken = default) =>
        Opportunities
            .AsNoTracking()
            .CountAsync(
                opportunity => opportunity.OrganizationId == organizationId
                    && opportunity.Status == status,
                cancellationToken);

    public void Add(VolunteerOpportunity opportunity)
    {
        ArgumentNullException.ThrowIfNull(opportunity);

        Opportunities.Add(opportunity);
    }
}

/// <summary>Reads and writes volunteer applications.</summary>
public sealed class VolunteerApplicationRepository : IVolunteerApplicationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public VolunteerApplicationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<VolunteerApplication> Applications => _dbContext.Set<VolunteerApplication>();

    public async Task<VolunteerApplication?> GetAsync(
        VolunteerApplicationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Applications.FirstOrDefaultAsync(
            application => application.Id == id, cancellationToken);
    }

    public async Task<VolunteerApplication?> FindForVolunteerAsync(
        VolunteerOpportunityId opportunityId,
        UserId volunteerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opportunityId);
        ArgumentNullException.ThrowIfNull(volunteerUserId);

        return await Applications.FirstOrDefaultAsync(
            application => application.OpportunityId == opportunityId
                && application.VolunteerUserId == volunteerUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<VolunteerApplication>> FindForVolunteerAsync(
        UserId volunteerUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volunteerUserId);

        return await Applications
            .AsNoTracking()
            .Where(application => application.VolunteerUserId == volunteerUserId)
            .OrderByDescending(application => application.CreatedAt)
            .ThenBy(application => application.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<VolunteerApplication>> SearchAsync(
        ApplicationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Applications.AsNoTracking();

        if (filter.OpportunityId is { } opportunityId)
        {
            query = query.Where(application => application.OpportunityId == opportunityId);
        }

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(application => application.OrganizationId == organizationId);
        }

        if (filter.VolunteerUserId is { } volunteerUserId)
        {
            query = query.Where(application => application.VolunteerUserId == volunteerUserId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(application => statuses.Contains(application.Status));
        }

        var cursor = CommunityProgrammeCursor.Parse(filter.Cursor);

        // The boundary is applied inside MaterialiseOldestFirstAsync and deliberately not here.
        //
        // **This is where the first version of this method was wrong, and the test that caught it is
        // worth keeping in mind.** CursorPage.Apply writes the newest-first predicate — everything
        // strictly *before* the boundary — and the oldest-first materialiser writes everything
        // strictly *after* it. Calling both produced `CreatedAt < @b AND CreatedAt > @b`, which no row
        // satisfies: page one served two applicants and page two served nothing, so five applications
        // paged as two. Nothing errored, and a suite that only asserted "the page is not empty" would
        // have passed.
        //
        // Oldest first, and this is the one page in the module that does not run newest-first: an
        // applicant queue is a queue, and the person who has waited longest is the person to answer.
        query = query
            .OrderBy(application => application.CreatedAt)
            .ThenBy(application => application.Id);

        return await MaterialiseOldestFirstAsync(
            query,
            cursor,
            Math.Clamp(filter.Limit, 1, CommunityProgrammeLimits.MaxPageSize),
            cancellationToken);
    }

    public async Task<VolunteerApplication?> FindByIdempotencyKeyAsync(
        UserId volunteerUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Applications.FirstOrDefaultAsync(
            application => application.VolunteerUserId == volunteerUserId
                && application.IdempotencyKey == normalized,
            cancellationToken);
    }

    public async Task<IReadOnlyList<VolunteerApplicationId>> FindWaitlistOrderAsync(
        VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opportunityId);

        return await Applications
            .AsNoTracking()
            .Where(application => application.OpportunityId == opportunityId
                && application.Status == VolunteerApplicationStatus.Waitlisted)
            .OrderBy(application => application.WaitlistedAt)
            .ThenBy(application => application.CreatedAt)
            .ThenBy(application => application.Id)
            .Select(application => application.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<OpportunityCounts> CountForOpportunityAsync(
        VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opportunityId);

        var rows = await Applications
            .AsNoTracking()
            .Where(application => application.OpportunityId == opportunityId)
            .GroupBy(application => application.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var byStatus = rows.ToDictionary(row => row.Status, row => row.Count);

        int Count(VolunteerApplicationStatus status) =>
            byStatus.TryGetValue(status, out var value) ? value : 0;

        return new OpportunityCounts
        {
            Filled = Count(VolunteerApplicationStatus.Approved)
                + Count(VolunteerApplicationStatus.Completed),
            Waiting = Count(VolunteerApplicationStatus.Waitlisted),
            Pending = Count(VolunteerApplicationStatus.Pending)
        };
    }

    public Task<int> CountPendingAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var pending = new[]
        {
            VolunteerApplicationStatus.Pending,
            VolunteerApplicationStatus.Waitlisted
        };

        return Applications
            .AsNoTracking()
            .CountAsync(
                application => application.OrganizationId == organizationId
                    && pending.Contains(application.Status),
                cancellationToken);
    }

    public void Add(VolunteerApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        Applications.Add(application);
    }

    /// <summary>
    /// The oldest-first counterpart of <see cref="CursorPage.MaterialiseAsync"/>.
    /// <para>
    /// The boundary condition is the mirror image — <c>&gt; boundary</c> rather than <c>&lt;</c> —
    /// and getting it wrong hands the client the same page for ever. Written out here rather than
    /// parameterised into the shared helper, because a direction flag on a paging primitive is a
    /// flag somebody eventually passes the wrong way round.
    /// </para>
    /// </summary>
    private static async Task<CursorPagedResult<VolunteerApplication>> MaterialiseOldestFirstAsync(
        IQueryable<VolunteerApplication> query,
        CommunityProgrammeCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        if (cursor?.AsTimestamp() is { } boundary)
        {
            var exclusion = CursorBoundary.Exclusion(
                cursor.BoundaryKey, VolunteerApplicationId.From);

            var seen = exclusion.Ids;
            var exhausted = exclusion.IsExhausted;

            query = query.Where(application =>
                application.CreatedAt > boundary
                || (application.CreatedAt == boundary
                    && !exhausted
                    && !seen.Contains(application.Id)));
        }

        var rows = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? CommunityProgrammeCursor.FromTimestamp(
                    rows[^1].CreatedAt,
                    CursorBoundary.Key(
                        rows,
                        row => row.CreatedAt,
                        row => row.Id.Value,
                        cursor?.BoundaryKey,
                        cursor?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<VolunteerApplication>.Create(rows, next, hasMore);
    }
}

/// <summary>Reads and writes volunteer assignments and the certificates they produce.</summary>
public sealed class VolunteerAssignmentRepository : IVolunteerAssignmentRepository
{
    private readonly DiasporaDbContext _dbContext;

    public VolunteerAssignmentRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<VolunteerAssignment> Assignments => _dbContext.Set<VolunteerAssignment>();

    private DbSet<VolunteerCertificate> Certificates => _dbContext.Set<VolunteerCertificate>();

    public async Task<VolunteerAssignment?> GetAsync(
        VolunteerAssignmentId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Assignments.FirstOrDefaultAsync(
            assignment => assignment.Id == id, cancellationToken);
    }

    public async Task<VolunteerAssignment?> FindForApplicationAsync(
        VolunteerApplicationId applicationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationId);

        return await Assignments.FirstOrDefaultAsync(
            assignment => assignment.ApplicationId == applicationId, cancellationToken);
    }

    public async Task<IReadOnlyList<VolunteerAssignment>> FindForVolunteerAsync(
        UserId volunteerUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volunteerUserId);

        return await Assignments
            .AsNoTracking()
            .Where(assignment => assignment.VolunteerUserId == volunteerUserId)
            .OrderByDescending(assignment => assignment.StartsAt)
            .ThenBy(assignment => assignment.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VolunteerAssignment>> FindConfirmedForVolunteerAsync(
        UserId volunteerUserId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volunteerUserId);

        var query = Assignments
            .AsNoTracking()
            .Where(assignment => assignment.VolunteerUserId == volunteerUserId
                && assignment.ConfirmedHours != null
                && assignment.ConfirmedAt != null);

        if (from is { } start)
        {
            query = query.Where(assignment => assignment.ConfirmedAt >= start);
        }

        if (to is { } end)
        {
            query = query.Where(assignment => assignment.ConfirmedAt <= end);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VolunteerAssignment>> FindConfirmedForOrganizationAsync(
        Guid organizationId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await Assignments
            .AsNoTracking()
            .Where(assignment => assignment.OrganizationId == organizationId
                && assignment.ConfirmedHours != null
                && assignment.ConfirmedAt != null
                && assignment.ConfirmedAt >= from
                && assignment.ConfirmedAt <= to)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<VolunteerShiftId, int>> CountByShiftAsync(
        VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(opportunityId);

        var occupying = new[]
        {
            VolunteerAssignmentStatus.Assigned,
            VolunteerAssignmentStatus.Confirmed,
            VolunteerAssignmentStatus.CheckedIn,
            VolunteerAssignmentStatus.Completed
        };

        var rows = await Assignments
            .AsNoTracking()
            .Where(assignment => assignment.OpportunityId == opportunityId
                && assignment.ShiftId != null
                && occupying.Contains(assignment.Status))
            .GroupBy(assignment => assignment.ShiftId!)
            .Select(group => new { ShiftId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ShiftId, row => row.Count);
    }

    public void Add(VolunteerAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        Assignments.Add(assignment);
    }

    public async Task<IReadOnlyList<VolunteerCertificate>> FindCertificatesForVolunteerAsync(
        UserId volunteerUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(volunteerUserId);

        return await Certificates
            .AsNoTracking()
            .Where(certificate => certificate.VolunteerUserId == volunteerUserId)
            .OrderByDescending(certificate => certificate.IssuedAt)
            .ThenBy(certificate => certificate.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<VolunteerCertificate?> FindCertificateForAssignmentAsync(
        VolunteerAssignmentId assignmentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assignmentId);

        return await Certificates.FirstOrDefaultAsync(
            certificate => certificate.AssignmentId == assignmentId, cancellationToken);
    }

    public void AddCertificate(VolunteerCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        Certificates.Add(certificate);
    }
}
