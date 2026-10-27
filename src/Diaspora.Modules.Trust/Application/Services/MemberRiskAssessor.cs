using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Options;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// Records evidence against a member and reassesses their risk from it (RWD-14, CS-4b).
/// <para>
/// <b>The only writer of <see cref="TrustProfile.Risk"/>.</b> An assessment is always the evidence on
/// record read through <see cref="RiskPolicy"/>, whether a consumer has just recorded a piece or the
/// sweep is letting old pieces age out, so two callers cannot arrive at two different opinions of one
/// member. A change of level is announced by the profile, and the Financial Platform holds the
/// member's points when it becomes High.
/// </para>
/// <para>
/// <b>Behind <c>Trust:EnableRiskAssessment</c>.</b> Off, nothing is recorded and nothing reassessed on
/// new evidence. The sweep's <see cref="ReassessAsync"/> is not switched: it can only lower an
/// assessment made while the switch was on.
/// </para>
/// </summary>
public sealed class MemberRiskAssessor
{
    private readonly IRiskSignalRepository _signals;
    private readonly ITrustProfileRepository _profiles;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly IOptions<TrustOptions> _options;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MemberRiskAssessor(
        IRiskSignalRepository signals,
        ITrustProfileRepository profiles,
        TrustProfileProvisioner provisioner,
        IOptions<TrustOptions> options,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _signals = signals;
        _profiles = profiles;
        _provisioner = provisioner;
        _options = options;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Records one piece of evidence, once, and reassesses its member, in one save.
    /// <para>
    /// Evidence already on record is not stored again, and the member is reassessed all the same, so a
    /// redelivery after a failed save still lands the assessment. Evidence older than every window
    /// would count toward nothing and is not kept.
    /// </para>
    /// </summary>
    public async Task RecordAsync(RiskSignal signal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (!_options.Value.EnableRiskAssessment)
        {
            return;
        }

        var now = _clock.UtcNow;
        var since = now - RiskPolicy.LongestWindow;

        if (signal.OccurredAt <= since)
        {
            return;
        }

        var profile = await _provisioner.GetOrCreateAsync(signal.UserId, cancellationToken);
        var evidence = (await _signals.ListSinceAsync(signal.UserId, since, cancellationToken)).ToList();

        if (!await _signals.ExistsAsync(signal.SourceKey, cancellationToken))
        {
            await _signals.AddAsync(signal, cancellationToken);
            evidence.Add(signal);
        }

        profile.UpdateRisk(RiskPolicy.Assess(evidence, profile.Grants, now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reassesses a member from the evidence on record, for the sweep, which saves its batch. False
    /// when the member has no profile.
    /// </summary>
    public async Task<bool> ReassessAsync(UserId member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);

        var profile = await _profiles.GetByUserAsync(member, cancellationToken);

        if (profile is null)
        {
            return false;
        }

        var now = _clock.UtcNow;
        var evidence = await _signals.ListSinceAsync(member, now - RiskPolicy.LongestWindow, cancellationToken);

        profile.UpdateRisk(RiskPolicy.Assess(evidence, profile.Grants, now));

        return true;
    }
}
