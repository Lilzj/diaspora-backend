using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Interviews;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>EF Core repository for interviews.</summary>
public sealed class InterviewRepository : IInterviewRepository
{
    private readonly DiasporaDbContext _dbContext;

    public InterviewRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Interview> Interviews => _dbContext.Set<Interview>();

    public async Task<Interview?> GetAsync(
        InterviewId id, CancellationToken cancellationToken = default) =>
        await Interviews
            .Include(interview => interview.Participants)
            .FirstOrDefaultAsync(interview => interview.Id == id, cancellationToken);

    public async Task<Interview?> GetForReadAsync(
        InterviewId id, CancellationToken cancellationToken = default) =>
        await Interviews.AsNoTracking()
            .Include(interview => interview.Participants)
            .FirstOrDefaultAsync(interview => interview.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Interview>> GetForApplicationAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationId);

        return await Interviews.AsNoTracking()
            .Include(interview => interview.Participants)
            .Where(interview => interview.ApplicationId == applicationId)
            .OrderBy(interview => interview.RoundNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetHighestRoundAsync(
        JobApplicationId applicationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(applicationId);

        // A scalar rather than loading every previous round to look at one number.
        var rounds = await Interviews.AsNoTracking()
            .Where(interview => interview.ApplicationId == applicationId)
            .Select(interview => interview.RoundNumber)
            .ToListAsync(cancellationToken);

        return rounds.Count == 0 ? 0 : rounds.Max();
    }

    public async Task<IReadOnlyList<Interview>> GetUpcomingForEmployerAsync(
        EmployerProfileId employerId,
        DateTimeOffset from,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(employerId);

        return await Upcoming(from)
            .Where(interview => interview.EmployerId == employerId)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Interview>> GetUpcomingForCandidateAsync(
        CandidateProfileId candidateId,
        DateTimeOffset from,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidateId);

        return await Upcoming(from)
            .Where(interview => interview.CandidateId == candidateId)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Interview>> GetAwaitingOutcomeAsync(
        DateTimeOffset endedBefore, int limit, CancellationToken cancellationToken = default) =>
        await Interviews
            .Where(interview =>
                !interview.IsCancelled
                && interview.Outcome == InterviewOutcome.Pending
                && interview.Schedule.ScheduledAt < endedBefore)
            .OrderBy(interview => interview.Schedule.ScheduledAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        Interview interview, CancellationToken cancellationToken = default) =>
        await Interviews.AddAsync(interview, cancellationToken);

    public void Remove(Interview interview) => Interviews.Remove(interview);

    /// <summary>Still ahead, still on, no outcome yet. Soonest first.</summary>
    private IQueryable<Interview> Upcoming(DateTimeOffset from) =>
        Interviews.AsNoTracking()
            .Include(interview => interview.Participants)
            .Where(interview =>
                !interview.IsCancelled
                && interview.Outcome == InterviewOutcome.Pending
                && interview.Schedule.ScheduledAt >= from)
            .OrderBy(interview => interview.Schedule.ScheduledAt);
}
