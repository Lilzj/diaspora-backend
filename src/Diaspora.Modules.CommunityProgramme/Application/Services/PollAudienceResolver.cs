using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Options;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityProgramme.Application.Services;

/// <summary>
/// Who may see a poll, who may vote in it, and how one member is recognised twice without being
/// identified once.
/// <para>
/// <b>Both questions are the server's, and neither is answerable on a device.</b> Audience membership
/// depends on B12's organization roll; one-member-one-vote depends on every other vote already cast.
/// The register asks for a 403 outside the audience and a 409 on a repeat, and both come from here.
/// </para>
/// </summary>
public sealed class PollAudienceResolver
{
    private readonly CommunityProgrammeAccessService _access;
    private readonly IOptionsMonitor<CommunityProgrammeOptions> _options;

    public PollAudienceResolver(
        CommunityProgrammeAccessService access,
        IOptionsMonitor<CommunityProgrammeOptions> options)
    {
        _access = access;
        _options = options;
    }

    /// <summary>
    /// Whether the caller is inside a poll's audience.
    /// <para>
    /// <b>A neighbourhood or country poll is open to any signed-in member.</b> The platform does not
    /// hold a verified residence for a member, so gating on one would be gating on a fact nobody can
    /// check — and a poll about a neighbourhood that refused everybody would be worse than one that
    /// asks broadly. The audience is carried on the poll so a later phase can narrow it the day B4
    /// can answer "does this person live here"; that is recorded as a documented limit rather than a
    /// silent one.
    /// </para>
    /// </summary>
    public async Task<bool> IsInAudienceAsync(
        CommunityPoll poll, UserId? caller, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(poll);

        if (poll.Audience == PollAudience.Public)
        {
            return true;
        }

        if (caller is null)
        {
            return false;
        }

        return poll.Audience switch
        {
            PollAudience.OrganizationMembers =>
                poll.OrganizationId is { } organizationId
                && (await _access.MemberOfAsync(cancellationToken)).Contains(organizationId),

            PollAudience.Invited => poll.IsInvited(caller),

            // See the summary: signed in is the honest floor until residence is checkable.
            PollAudience.Neighborhood or PollAudience.Country => true,

            _ => false
        };
    }

    /// <summary>
    /// Why the caller cannot vote, or null when they can.
    /// <para>
    /// A sentence rather than a code, because the client prints it verbatim — §51's "Poll closed" and
    /// "Already voted" are the server's words, and a client composing its own would be describing a
    /// decision it did not make.
    /// </para>
    /// </summary>
    public async Task<string?> CannotVoteReasonAsync(
        CommunityPoll poll,
        UserId? caller,
        bool hasVoted,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(poll);

        if (caller is null)
        {
            return "Sign in to vote.";
        }

        if (!poll.AcceptsVotes)
        {
            return poll.Status switch
            {
                PollStatus.Draft => "This poll has not opened yet.",
                PollStatus.Scheduled => "This poll has not opened yet.",
                PollStatus.Cancelled => "This poll was cancelled.",
                _ => "This poll has closed."
            };
        }

        if (!await IsInAudienceAsync(poll, caller, cancellationToken))
        {
            return "This poll is not open to you.";
        }

        if (hasVoted && !poll.AllowsChange)
        {
            return "You have already voted.";
        }

        return null;
    }

    /// <summary>
    /// A poll-scoped, keyed hash of the voter.
    /// <para>
    /// <b>What makes one-member-one-vote survive anonymity.</b> An anonymous poll stores no voter
    /// identifier at all, so the uniqueness rule needs something else to collide on. This is that
    /// something: keyed, so it cannot be computed by anybody without the deployment's secret; scoped
    /// to the poll, so the same member's fingerprints on two polls cannot be correlated; and one-way,
    /// so a row that carries it still carries nothing that identifies a person.
    /// </para>
    /// <para>
    /// <b>Used on every poll, not only anonymous ones.</b> One index, one rule, and no way to satisfy
    /// it for one kind of poll and forget it for the other.
    /// </para>
    /// </summary>
    public string Fingerprint(CommunityPollId pollId, UserId voter)
    {
        ArgumentNullException.ThrowIfNull(pollId);
        ArgumentNullException.ThrowIfNull(voter);

        var secret = _options.CurrentValue.PollFingerprintSecret;
        var key = Encoding.UTF8.GetBytes(secret);

        var payload = Encoding.UTF8.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{pollId.Value:N}:{voter.Value:N}"));

        return Convert.ToBase64String(HMACSHA256.HashData(key, payload));
    }
}
