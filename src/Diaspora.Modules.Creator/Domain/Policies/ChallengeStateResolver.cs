using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Enums;

namespace Diaspora.Modules.Creator.Domain.Policies;

/// <summary>
/// Resolves the one state a client sees from the challenge's half and the member's half, and maps
/// the <c>state</c> filter's groups onto it.
/// <para>
/// <b>The register asks for the groups explicitly:</b> "The <c>state</c> parameter accepts
/// <b>groups</b> (<c>active</c>, <c>open</c>, <c>finished</c>) as well as members, so a three-tab
/// screen makes one request rather than three." §9 of the phase brief adds the obligation this
/// class exists to discharge: "Verify that the state groups map deterministically to the correct
/// states."
/// </para>
/// <para>
/// <b>Deterministic means the three groups partition the nine states with no overlap and no gap.</b>
/// A unit test asserts exactly that against <see cref="Groups"/>, because a challenge that appeared
/// in two tabs or in none is the failure a hand-written filter produces on its fourth state.
/// </para>
/// </summary>
public static class ChallengeStateResolver
{
    public const string ActiveGroup = "active";
    public const string OpenGroup = "open";
    public const string FinishedGroup = "finished";

    /// <summary>
    /// The partition. Every member of <see cref="CreatorChallengeState"/> appears in exactly one.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<CreatorChallengeState>> Groups =
        new Dictionary<string, IReadOnlyList<CreatorChallengeState>>(StringComparer.OrdinalIgnoreCase)
        {
            // Not joined, and joinable — or about to be.
            [OpenGroup] =
            [
                CreatorChallengeState.Upcoming,
                CreatorChallengeState.Open
            ],

            // Joined and still running.
            [ActiveGroup] =
            [
                CreatorChallengeState.Joined,
                CreatorChallengeState.InProgress
            ],

            // Over, one way or another.
            [FinishedGroup] =
            [
                CreatorChallengeState.Completed,
                CreatorChallengeState.Claimed,
                CreatorChallengeState.Failed,
                CreatorChallengeState.Expired,
                CreatorChallengeState.Cancelled
            ]
        };

    /// <summary>
    /// What this member sees this challenge as.
    /// <para>
    /// The enrolment wins wherever it has an opinion, because a member who joined has a state of
    /// their own; the programme's state is what somebody who has not joined sees. The one exception
    /// is a cancelled challenge, which is cancelled for everybody including the people part-way
    /// through it.
    /// </para>
    /// </summary>
    public static CreatorChallengeState Resolve(
        CreatorChallenge challenge, CreatorChallengeEnrollment? enrollment, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        var programme = challenge.ProgrammeState(now);

        if (programme == CreatorChallengeState.Cancelled)
        {
            return CreatorChallengeState.Cancelled;
        }

        if (enrollment is null)
        {
            return programme;
        }

        // A member part-way through a challenge that has ended has failed it rather than being
        // returned to "expired", which reads as though the platform lost their attempt.
        if (programme == CreatorChallengeState.Expired
            && enrollment.State is CreatorChallengeState.Joined or CreatorChallengeState.InProgress)
        {
            return CreatorChallengeState.Failed;
        }

        return enrollment.State;
    }

    /// <summary>
    /// Reads a <c>state</c> query value into the set of states it means.
    /// <para>
    /// Accepts a group name or a state name, case-insensitively. An unrecognised value yields null,
    /// which the query treats as "no filter" — a stale client asking for a state this version does
    /// not have gets the unfiltered list rather than an empty one, which is the kinder failure and
    /// the one that does not look like a broken screen.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CreatorChallengeState>? Parse(string? state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        var value = state.Trim();

        if (Groups.TryGetValue(value, out var group))
        {
            return group;
        }

        return Enum.TryParse<CreatorChallengeState>(value, ignoreCase: true, out var single)
            ? [single]
            : null;
    }
}
