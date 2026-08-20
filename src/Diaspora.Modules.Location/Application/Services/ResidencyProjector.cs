using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Observability;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Application.Services;

/// <summary>
/// Applies a Trust decision to the residency projection.
/// <para>
/// Shared by all four verification event handlers rather than repeated in each. The
/// get-or-create and the out-of-order guard are the same in every one of them, and four
/// copies of an ordering check is three chances to omit it — which would let a retried
/// approval resurrect a revocation.
/// </para>
/// </summary>
public sealed class ResidencyProjector
{
    private readonly IVerifiedResidencyRepository _residencies;

    public ResidencyProjector(IVerifiedResidencyRepository residencies)
    {
        _residencies = residencies;
    }

    /// <summary>
    /// Records the decision, creating the projection row if this is the first news of it.
    /// </summary>
    /// <returns>True when the projection changed and a save is warranted.</returns>
    public async Task<bool> ApplyAsync(
        Guid userId,
        string countryCode,
        ResidencyVerificationState state,
        Guid? verificationId,
        DateTimeOffset decidedAt,
        DateTimeOffset observedAt,
        DateTimeOffset? expiresAt,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var code = CountryCode.TryCreate(countryCode);

        if (code.IsFailure)
        {
            return false;
        }

        var user = UserId.From(userId);
        var residency = await _residencies.GetAsync(user, code.Value, cancellationToken);

        if (residency is null)
        {
            residency = VerifiedResidency.For(user, code.Value);
            await _residencies.AddAsync(residency, cancellationToken);
        }

        var changed = residency.Apply(
            state, verificationId, decidedAt, observedAt, expiresAt, reason);

        if (changed)
        {
            LocationTelemetry.ResidencyProjections.Add(
                1, new KeyValuePair<string, object?>("state", state.ToString()));
        }

        return changed;
    }
}
