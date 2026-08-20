namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// What kind of thing is being recommended.
/// <para>
/// <b>This is the module's entire vocabulary for other people's data, and it is deliberately a
/// closed list of names rather than an open one.</b> A target type plus an identifier is all this
/// module ever holds about a job or a flat; every attribute it ranks on is projected from a
/// published contract into a candidate row. Adding a vertical means adding a member here and a
/// consumer that projects it — never a reference to that vertical's assembly.
/// </para>
/// <para>
/// <see cref="Country"/> and <see cref="Announcement"/> are declared and not yet produced. The brief
/// names both as future work; declaring them now means the strategy code, the policy code and the
/// API surface already handle them, so landing them later is a consumer and a catalogue row rather
/// than a change to any of the three.
/// </para>
/// </summary>
public enum RecommendationTargetType
{
    Job = 0,
    MarketplaceListing = 1,
    HousingListing = 2,
    Provider = 3,
    Guide = 4,
    LearningPath = 5,
    Organization = 6,
    Event = 7,
    Neighborhood = 8,
    Community = 9,

    /// <summary>Declared, not yet produced. See the class summary.</summary>
    Country = 10,

    /// <summary>Declared, not yet produced. See the class summary.</summary>
    Announcement = 11
}
