using Diaspora.Modules.CommunityLife.Extensibility.Catalog;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Catalog;

/// <summary>
/// The event categories every deployment starts with.
/// <para>
/// <b><c>DefaultsToRsvp</c> is the field that earns its place here.</b> A Sunday service does not
/// take a headcount and a workshop with twenty seats does; putting the answer in the catalogue means
/// an organizer is asked the right question without knowing there was a question, and a category
/// added next year behaves correctly in an app shipped this year.
/// </para>
/// <para>
/// <b>The facets are what the filter bar is built from.</b> They are deliberately few and overlapping
/// — a naming ceremony is family <em>and</em> cultural — because a member filtering a busy weekend
/// wants a handful of broad shelves, not a taxonomy.
/// </para>
/// </summary>
public sealed class DefaultEventCategoryContributor : IEventCategoryContributor
{
    public IEnumerable<EventCategoryDescriptor> GetEventCategories() =>
    [
        new()
        {
            Key = "worship-service",
            Name = "Worship service",
            Description = "A regular service, prayer meeting or vigil.",
            IconName = "place-of-worship",
            GroupName = "Faith",
            Facets = ["religious"],
            Order = 10,

            // Nobody signs up for a service. Asking would make the commonest recurring event on the
            // platform the most annoying one to publish.
            DefaultsToRsvp = false
        },
        new()
        {
            Key = "religious-festival",
            Name = "Religious festival",
            Description = "Eid, Christmas, Easter, Diwali and other observances.",
            IconName = "star-and-crescent",
            GroupName = "Faith",
            Facets = ["religious", "family", "cultural"],
            Order = 20,
            DefaultsToRsvp = false
        },
        new()
        {
            Key = "cultural-celebration",
            Name = "Cultural celebration",
            Description = "Independence days, heritage nights and cultural festivals.",
            IconName = "flag",
            GroupName = "Culture",
            Facets = ["cultural", "family"],
            Order = 30
        },
        new()
        {
            Key = "naming-ceremony",
            Name = "Naming ceremony",
            Description = "A naming, christening or dedication.",
            IconName = "baby",
            GroupName = "Family",
            Facets = ["family", "cultural"],
            Order = 40
        },
        new()
        {
            Key = "wedding",
            Name = "Wedding",
            Description = "A wedding, engagement or traditional marriage.",
            IconName = "rings-wedding",
            GroupName = "Family",
            Facets = ["family", "cultural"],
            Order = 50
        },
        new()
        {
            Key = "funeral",
            Name = "Funeral or memorial",
            Description = "A funeral, wake or memorial gathering.",
            IconName = "dove",
            GroupName = "Family",
            Facets = ["family"],
            Order = 60,

            // A guest list at a funeral is the organizer's business. Nobody should be asked to
            // confirm attendance at a burial.
            DefaultsToRsvp = false
        },
        new()
        {
            Key = "community-meeting",
            Name = "Community meeting",
            Description = "An association meeting, town hall or AGM.",
            IconName = "users",
            GroupName = "Community",
            Facets = ["cultural"],
            Order = 70
        },
        new()
        {
            Key = "fundraiser",
            Name = "Fundraiser",
            Description = "A fundraising dinner, appeal or charity night.",
            IconName = "hand-holding-heart",
            GroupName = "Community",
            Facets = ["cultural", "volunteer"],
            Order = 80
        },
        new()
        {
            Key = "workshop",
            Name = "Workshop or training",
            Description = "Skills, language, immigration advice and other training.",
            IconName = "chalkboard-teacher",
            GroupName = "Learning",
            Facets = ["student", "volunteer"],
            Order = 90
        },
        new()
        {
            Key = "networking",
            Name = "Networking",
            Description = "A business mixer, professional meet-up or trade evening.",
            IconName = "handshake",
            GroupName = "Professional",
            Facets = ["business"],
            Order = 100
        },
        new()
        {
            Key = "career-fair",
            Name = "Career fair",
            Description = "A jobs fair, recruitment day or careers evening.",
            IconName = "briefcase",
            GroupName = "Professional",
            Facets = ["business", "student"],
            Order = 110
        },
        new()
        {
            Key = "market",
            Name = "Market or bazaar",
            Description = "A food market, craft fair or pop-up bazaar.",
            IconName = "store",
            GroupName = "Community",
            Facets = ["business", "family"],
            Order = 120,
            DefaultsToRsvp = false
        },
        new()
        {
            Key = "sports",
            Name = "Sport",
            Description = "A match, tournament or training session.",
            IconName = "futbol",
            GroupName = "Sport",
            Facets = ["sport", "family"],
            Order = 130
        },
        new()
        {
            Key = "student-event",
            Name = "Student event",
            Description = "A society social, freshers event or student mixer.",
            IconName = "graduation-cap",
            GroupName = "Learning",
            Facets = ["student"],
            Order = 140
        },
        new()
        {
            Key = "volunteering",
            Name = "Volunteering",
            Description = "A clean-up, food drive or volunteering session.",
            IconName = "hands-helping",
            GroupName = "Community",
            Facets = ["volunteer"],
            Order = 150
        },
        new()
        {
            Key = "support-session",
            Name = "Support session",
            Description = "Peer support, bereavement, health and wellbeing.",
            IconName = "heart",
            GroupName = "Support",
            Facets = ["volunteer"],
            Order = 160
        },
        new()
        {
            Key = "other",
            Name = "Other",
            Description = "Anything the list above does not cover.",
            IconName = "circle",
            GroupName = "Other",
            Order = 999
        }
    ];
}
