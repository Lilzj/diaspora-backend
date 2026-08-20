using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Finds a free deep-link handle.
/// <para>
/// Two restaurants genuinely called "Mama's Kitchen" is not an error, and neither is a provider
/// whose name transliterates to nothing — "مطعم الشام" slugifies to an empty string. Both land
/// here and both come out with a usable handle.
/// </para>
/// <para>
/// <b>The database's unique index is the real guarantee; this is an optimisation.</b> A
/// check-then-insert is a race by construction, so the index still has to exist and still has to
/// be able to refuse. What this avoids is the ordinary case — two people registering similarly
/// named businesses on the same afternoon — turning into a constraint violation, a rolled-back
/// transaction and an error in the log every time.
/// </para>
/// </summary>
public sealed class ProviderSlugAllocator
{
    /// <summary>
    /// How many suffixes to try before giving up. Ten is far past any real collision; a
    /// deployment that needed an eleventh "mamas-kitchen" has a different problem.
    /// </summary>
    private const int MaxAttempts = 10;

    private readonly IProviderRepository _providers;

    public ProviderSlugAllocator(IProviderRepository providers) => _providers = providers;

    /// <summary>
    /// Allocates a handle, from the caller's choice or derived from the name.
    /// <para>
    /// A caller-supplied slug that is taken <em>fails</em> rather than being silently suffixed:
    /// somebody who typed a handle wants that handle, and quietly giving them
    /// <c>mamas-kitchen-2</c> is worse than telling them it is gone. A derived slug is suffixed
    /// without comment, because nobody chose it.
    /// </para>
    /// </summary>
    public async Task<Result<ProviderSlug>> AllocateAsync(
        string? requestedSlug, ProviderName name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!string.IsNullOrWhiteSpace(requestedSlug))
        {
            var requested = ProviderSlug.Create(requestedSlug);

            if (requested.IsFailure)
            {
                return requested;
            }

            return await _providers.SlugExistsAsync(requested.Value, cancellationToken)
                ? Result.Failure<ProviderSlug>(ProviderErrors.SlugTaken)
                : requested;
        }

        var candidate = ProviderSlug.FromName(name);

        if (!await _providers.SlugExistsAsync(candidate, cancellationToken))
        {
            return Result.Success(candidate);
        }

        for (var suffix = 2; suffix <= MaxAttempts + 1; suffix++)
        {
            var suffixed = candidate.WithSuffix(suffix);

            if (!await _providers.SlugExistsAsync(suffixed, cancellationToken))
            {
                return Result.Success(suffixed);
            }
        }

        return Result.Failure<ProviderSlug>(ProviderErrors.SlugTaken);
    }
}
