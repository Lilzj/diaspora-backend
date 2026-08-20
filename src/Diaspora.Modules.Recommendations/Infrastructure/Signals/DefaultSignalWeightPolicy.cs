using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Signals;

namespace Diaspora.Modules.Recommendations.Infrastructure.Signals;

/// <summary>
/// The shipped theory of how much each observation means.
/// <para>
/// <b>Every number here is an assertion about human behaviour, and they are gathered in one file so
/// that the assertions can be argued with.</b> Scattering them across the consumers that produce
/// signals would make the platform's model of intent something you could only reconstruct by
/// reading forty classes.
/// </para>
/// <para>
/// <b>The shape of the ladder: attention, then interest, then commitment.</b> A view is a glance and
/// is worth little, because most views are mistakes. A save is a deliberate act with no cost. An
/// application is an evening's work and a real commitment. A transaction is money. The gaps between
/// them are wide on purpose — a member who applied for one job and viewed thirty should be
/// recommended things like the one, not like the thirty.
/// </para>
/// <para>
/// <b>The half-lives encode how fast each kind of fact goes stale, and they differ by two orders of
/// magnitude.</b> A country lasts years. A search term is worthless in a fortnight — somebody who
/// searched for "airport transfer" last month has taken the flight.
/// </para>
/// <para>
/// <b>A client is trusted least.</b> Its ceiling is deliberately below the weight of the strongest
/// kinds, so a modified app cannot claim a member transacted with something merely by recording a
/// signal that says so. Only a module publishing a contract can assert commitment.
/// </para>
/// </summary>
public sealed class DefaultSignalWeightPolicy : ISignalWeightPolicy
{
    public decimal WeightFor(SignalKind kind) => kind switch
    {
        // Facts about the member, not about intent. Weighted at the ceiling because they are
        // certainties rather than inferences.
        SignalKind.Location => 1.0m,
        SignalKind.Language => 1.0m,
        SignalKind.Trust => 1.0m,
        SignalKind.Verification => 1.0m,
        SignalKind.Onboarding => 1.0m,

        // The member said so.
        SignalKind.Interest => 0.95m,
        SignalKind.Preference => 0.95m,

        // Commitment.
        SignalKind.Transacted => 0.9m,
        SignalKind.Applied => 0.8m,
        SignalKind.LearningCompleted => 0.75m,
        SignalKind.Joined => 0.7m,
        SignalKind.Participated => 0.7m,

        // Interest without cost.
        SignalKind.Saved => 0.6m,
        SignalKind.Followed => 0.6m,
        SignalKind.LearningProgress => 0.5m,
        SignalKind.Engaged => 0.45m,
        SignalKind.RecommendationAccepted => 0.8m,

        // Attention.
        SignalKind.Searched => 0.35m,
        SignalKind.NotificationClicked => 0.3m,
        SignalKind.Viewed => 0.2m,
        SignalKind.NotificationOpened => 0.15m,

        // A refusal. Weighted high because it is the most informative single observation the
        // platform gets — the profile builder subtracts rather than adds on this kind.
        SignalKind.RecommendationDismissed => 0.7m,

        _ => 0.2m
    };

    public decimal CeilingFor(SignalSource source) => source switch
    {
        // A client can claim attention and nothing stronger. See the class summary.
        SignalSource.Client => 0.4m,

        // This module reporting on itself. Trusted, because it is the only writer.
        SignalSource.Recommendations => 1.0m,

        // A module publishing a contract. Trusted — the outbox is the authentication.
        _ => 1.0m
    };

    public TimeSpan HalfLifeFor(SignalKind kind) => kind switch
    {
        SignalKind.Location => TimeSpan.FromDays(365),
        SignalKind.Language => TimeSpan.FromDays(365),
        SignalKind.Trust => TimeSpan.FromDays(365),
        SignalKind.Verification => TimeSpan.FromDays(365),
        SignalKind.Onboarding => TimeSpan.FromDays(365),
        SignalKind.Interest => TimeSpan.FromDays(180),
        SignalKind.Preference => TimeSpan.FromDays(180),

        SignalKind.Transacted => TimeSpan.FromDays(120),
        SignalKind.Applied => TimeSpan.FromDays(90),
        SignalKind.LearningCompleted => TimeSpan.FromDays(120),
        SignalKind.Joined => TimeSpan.FromDays(120),
        SignalKind.Participated => TimeSpan.FromDays(90),

        SignalKind.Saved => TimeSpan.FromDays(60),
        SignalKind.Followed => TimeSpan.FromDays(120),
        SignalKind.LearningProgress => TimeSpan.FromDays(45),
        SignalKind.Engaged => TimeSpan.FromDays(45),

        SignalKind.RecommendationAccepted => TimeSpan.FromDays(60),
        SignalKind.RecommendationDismissed => TimeSpan.FromDays(90),

        SignalKind.NotificationClicked => TimeSpan.FromDays(30),
        SignalKind.NotificationOpened => TimeSpan.FromDays(21),
        SignalKind.Viewed => TimeSpan.FromDays(21),

        // Two weeks. Somebody who searched for an airport transfer has taken the flight.
        SignalKind.Searched => TimeSpan.FromDays(14),

        _ => TimeSpan.FromDays(30)
    };
}
