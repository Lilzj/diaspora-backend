using Diaspora.Modules.CommunityLife.Extensibility.Catalog;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Catalog;

/// <summary>
/// The organization types every deployment starts with.
/// <para>
/// <b>Chosen from what diaspora communities actually organize themselves into</b>, not from an
/// abstract taxonomy: a place of worship, a hometown association, a students' union, a women's group.
/// A platform that opened with "Non-profit / For-profit / Other" would leave the first ten
/// organizations filed under Other and the directory useless from the first week.
/// </para>
/// <para>
/// <b>Contributed rather than hard-coded into a seeder</b>, so a deployment can add its own without
/// forking, and can retire any of these through the catalogue command without a migration.
/// </para>
/// <para>
/// <b><c>EncouragesVerification</c> marks the types where a badge matters most.</b> A church, a
/// charity or a school is a body somebody hands money or children to, and the client uses this to
/// prompt for verification rather than waiting to be asked.
/// </para>
/// </summary>
public sealed class DefaultOrganizationTypeContributor : IOrganizationTypeContributor
{
    public IEnumerable<OrganizationTypeDescriptor> GetOrganizationTypes() =>
    [
        new()
        {
            Key = "place-of-worship",
            Name = "Place of worship",
            Description = "A church, mosque, temple or other congregation.",
            IconName = "place-of-worship",
            GroupName = "Faith",
            Order = 10,
            EncouragesVerification = true
        },
        new()
        {
            Key = "hometown-association",
            Name = "Hometown association",
            Description =
                "A body for people from one town, region or ethnic group back home.",
            IconName = "users",
            GroupName = "Community",
            Order = 20,
            EncouragesVerification = true
        },
        new()
        {
            Key = "cultural-association",
            Name = "Cultural association",
            Description = "Language, music, dance, heritage and the arts.",
            IconName = "masks",
            GroupName = "Community",
            Order = 30
        },
        new()
        {
            Key = "student-society",
            Name = "Student society",
            Description = "A university or college society.",
            IconName = "graduation-cap",
            GroupName = "Education",
            Order = 40
        },
        new()
        {
            Key = "womens-group",
            Name = "Women's group",
            Description = "A women's association, network or support group.",
            IconName = "female",
            GroupName = "Community",
            Order = 50
        },
        new()
        {
            Key = "youth-group",
            Name = "Youth group",
            Description = "A group for young people and their families.",
            IconName = "child",
            GroupName = "Community",
            Order = 60
        },
        new()
        {
            Key = "charity",
            Name = "Charity",
            Description = "A registered charity or relief organization.",
            IconName = "hand-holding-heart",
            GroupName = "Support",
            Order = 70,
            EncouragesVerification = true
        },
        new()
        {
            Key = "support-group",
            Name = "Support group",
            Description = "Health, bereavement, immigration and other peer support.",
            IconName = "hands-helping",
            GroupName = "Support",
            Order = 80
        },
        new()
        {
            Key = "business-network",
            Name = "Business network",
            Description = "A chamber, trade body or professional network.",
            IconName = "briefcase",
            GroupName = "Professional",
            Order = 90
        },
        new()
        {
            Key = "professional-body",
            Name = "Professional body",
            Description = "An association for one profession or trade.",
            IconName = "id-badge",
            GroupName = "Professional",
            Order = 100
        },
        new()
        {
            Key = "sports-club",
            Name = "Sports club",
            Description = "A club, team or league.",
            IconName = "futbol",
            GroupName = "Sport",
            Order = 110
        },
        new()
        {
            Key = "school",
            Name = "School",
            Description =
                "A supplementary school, language school or weekend academy.",
            IconName = "school",
            GroupName = "Education",
            Order = 120,
            EncouragesVerification = true
        },
        new()
        {
            Key = "media",
            Name = "Media",
            Description = "A community radio station, newspaper or channel.",
            IconName = "broadcast-tower",
            GroupName = "Media",
            Order = 130
        },
        new()
        {
            Key = "other",
            Name = "Other",
            Description = "Anything the list above does not cover.",
            IconName = "circle",
            GroupName = "Other",

            // Last, always. A catch-all that sorted anywhere else would be chosen by people who
            // stopped reading, and every one of those is a body nobody can find again.
            Order = 999
        }
    ];
}
