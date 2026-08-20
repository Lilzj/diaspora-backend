using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Application.Services;

/// <summary>
/// Finds a free deep-link handle for a posting or an employer.
/// <para>
/// <b>A caller-supplied handle that is taken is refused; a derived one that is taken gets a
/// suffix.</b> The asymmetry is deliberate. Somebody who asked for <c>senior-nurse-berlin</c> and
/// silently got <c>senior-nurse-berlin-2</c> has been given a link they did not ask for and will
/// share the wrong one. Somebody who supplied nothing has no expectation to violate.
/// </para>
/// <para>
/// The existence check is advisory — the unique index is what actually guarantees uniqueness.
/// This exists so the common case is a clean insert rather than a caught constraint violation and
/// a retry.
/// </para>
/// <para>
/// The suffix walk is bounded. Past the limit it falls back to a short random discriminator
/// rather than looping: a thousand employers posting "Nurse" in one city is not a reason to spin.
/// </para>
/// </summary>
public sealed class JobSlugAllocator
{
    /// <summary>How many numbered suffixes to try before falling back to a random one.</summary>
    private const int MaxNumberedAttempts = 20;

    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;

    public JobSlugAllocator(
        IJobPostingRepository postings, IEmployerProfileRepository employers)
    {
        _postings = postings;
        _employers = employers;
    }

    /// <summary>
    /// A free handle for a posting. Derived from the title plus whatever else distinguishes it —
    /// usually the employer name and the city, because "nurse" is a title a thousand employers
    /// will post and the first one should not take the handle for everyone.
    /// </summary>
    public async Task<Result<JobSlug>> AllocatePostingSlugAsync(
        string? requested,
        string title,
        string? employerName,
        string? cityName,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var explicitSlug = JobSlug.Create(requested);

            if (explicitSlug.IsFailure)
            {
                return explicitSlug;
            }

            return await _postings.SlugExistsAsync(explicitSlug.Value, cancellationToken)
                ? Result.Failure<JobSlug>(JobErrors.SlugTaken)
                : explicitSlug;
        }

        var derived = JobSlug.FromParts(title, employerName, cityName);

        if (derived.IsFailure)
        {
            return derived;
        }

        return await FindFreeAsync(
            derived.Value,
            candidate => _postings.SlugExistsAsync(candidate, cancellationToken));
    }

    /// <summary>A free handle for an employer, derived from its display name.</summary>
    public async Task<Result<JobSlug>> AllocateEmployerSlugAsync(
        string? requested, string displayName, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var explicitSlug = JobSlug.Create(requested);

            if (explicitSlug.IsFailure)
            {
                return explicitSlug;
            }

            return await _employers.SlugExistsAsync(explicitSlug.Value, cancellationToken)
                ? Result.Failure<JobSlug>(JobErrors.EmployerSlugTaken)
                : explicitSlug;
        }

        var derived = JobSlug.FromParts(displayName);

        if (derived.IsFailure)
        {
            return derived;
        }

        return await FindFreeAsync(
            derived.Value,
            candidate => _employers.SlugExistsAsync(candidate, cancellationToken));
    }

    private static async Task<Result<JobSlug>> FindFreeAsync(
        JobSlug seed, Func<JobSlug, Task<bool>> exists)
    {
        if (!await exists(seed))
        {
            return Result.Success(seed);
        }

        for (var attempt = 2; attempt <= MaxNumberedAttempts; attempt++)
        {
            var candidate = seed.WithSuffix(attempt.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

            if (!await exists(candidate))
            {
                return Result.Success(candidate);
            }
        }

        // A short random discriminator rather than an unbounded walk. Still checked once,
        // and the unique index is the real guarantee if even this collides.
        var random = seed.WithSuffix(Guid.NewGuid().ToString("N")[..6]);

        return await exists(random)
            ? Result.Failure<JobSlug>(JobErrors.SlugTaken)
            : Result.Success(random);
    }
}
