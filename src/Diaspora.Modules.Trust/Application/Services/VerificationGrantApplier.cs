using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Trust.Application.Services;

/// <summary>
/// What an approved verification does to a member's standing. The one implementation of it.
/// <para>
/// <b>There were two ways to approve a verification and only one of them granted anything.</b>
/// A reviewer's approval runs through <c>ReviewVerification</c>, which records the grant, applies
/// the handler's <see cref="IVerificationTypeHandler.ApprovalAward"/> and writes the timeline
/// entry. An automatic approval ran through <c>VerificationSubmissionCoordinator</c>, which
/// marked the request <c>Approved</c> and did none of those three — so the member would have held
/// an approved verification, no grant, no points, and nothing on their trust journey saying it
/// had happened.
/// </para>
/// <para>
/// That never bit because the automatic path was dead: only the phone and email handlers opt into
/// <see cref="IVerificationTypeHandler.SupportsAutomaticApproval"/>, and nothing submitted either
/// type. Connecting Identity's phone verification to Trust is what makes it live, which is why
/// the two halves are joined here first rather than duplicated there.
/// </para>
/// <para>
/// <b>It does not reconcile badges.</b> Both callers do that themselves, because the reviewer's
/// path reconciles after a rejection and a revocation too — badges follow standing in both
/// directions, and folding one direction in here would leave a caller reconciling twice for an
/// approval and once for everything else.
/// </para>
/// </summary>
public sealed class VerificationGrantApplier
{
    private readonly TrustProfileProvisioner _provisioner;
    private readonly ITrustEventRepository _timeline;
    private readonly VerificationTypeHandlerRegistry _handlers;
    private readonly ReputationService _reputation;

    public VerificationGrantApplier(
        TrustProfileProvisioner provisioner,
        ITrustEventRepository timeline,
        VerificationTypeHandlerRegistry handlers,
        ReputationService reputation)
    {
        _provisioner = provisioner;
        _timeline = timeline;
        _handlers = handlers;
        _reputation = reputation;
    }

    /// <summary>
    /// Records the grant, pays the approval award and writes the journey entry.
    /// <para>
    /// <paramref name="detail"/> is the sentence the member reads on their trust journey, and it
    /// is the caller's because the two paths are not the same event: one was decided by a person
    /// and the other by a rule, and telling a member a machine's decision was "approved by the
    /// compliance team" would be a lie about who looked at their documents.
    /// </para>
    /// <para>
    /// <b>The award is applied here and nowhere else for these types.</b> The award is keyed on the
    /// verification id, so applying the same approval twice pays once — but a second producer
    /// that keyed the same approval some other way would still be a second payment. Two such
    /// consumers were written and removed; see the header of <c>ReputationAwardConsumers.cs</c>.
    /// </para>
    /// </summary>
    public async Task<TrustProfile> ApplyApprovalAsync(
        VerificationRequest verification,
        string detail,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verification);

        var (profile, _) = await _provisioner.GetOrCreateBothAsync(
            verification.UserId, cancellationToken);

        var country = verification.TargetCountry ?? verification.ResidenceCountry;

        profile.RecordGrant(verification.Type, country, now, verification.ArrivalDate);

        if (_handlers.For(verification.Type)?.ApprovalAward is { } award)
        {
            await _reputation.ApplyAwardAsync(
                verification.UserId,
                award,
                ReputationAwardKey.Verification(award.ActionType, verification.Id.Value),
                "verification",
                verification.Id.Value.ToString(),
                country,
                cancellationToken);
        }

        await _timeline.AddAsync(
            TrustEvent.Record(
                verification.UserId,
                TrustEventTypes.VerificationApproved,
                $"{Humanize(verification.Type)} verified",
                now,
                detail,
                TrustEventTypes.IconFor(TrustEventTypes.VerificationApproved),
                country),
            cancellationToken);

        return profile;
    }

    /// <summary>"CommunityGuide" reads better to a member as "Community Guide".</summary>
    private static string Humanize(VerificationType type) =>
        System.Text.RegularExpressions.Regex.Replace(
            type.ToString(), "(?<!^)([A-Z])", " $1");
}
