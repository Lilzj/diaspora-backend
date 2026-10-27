namespace Diaspora.Modules.Creator.Domain.Policies;

/// <summary>
/// The policy version stamped on every points grant this module makes (RWD-13).
/// <para>
/// Stored on the Financial Platform's entry, so "why did this pay 100 and not 250" stays answerable
/// after the schedule changes. <b>A milestone and a challenge are each the schedule for their own
/// key</b>, and the catalogue seeder leaves existing rows alone, so a deployment's values can differ
/// from the seed code: each row carries its own revision rather than the module one version. The
/// sign-up bonus is one configured number, so the number is its version.
/// </para>
/// </summary>
public static class CreatorGrantPolicyVersions
{
    public static string Milestone(string key, int revision) => $"creator.milestone/{key}@{revision}";

    public static string Challenge(string key, int revision) => $"creator.challenge/{key}@{revision}";

    public static string SignupBonus(int points) => $"creator.signup-bonus@{points}";
}
