namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// What somebody actually gave.
/// <para>
/// <b>Three of these five are not money, and that is the point.</b> The client's donation-causes
/// screen offers monetary, goods, volunteer service, professional skills and supplies; a model that
/// assumed a payment would make four fifths of that screen unrepresentable. Only
/// <see cref="Monetary"/> produces ledger entries — a pledge of six Saturdays at a food bank is a
/// contribution with no money on it, and the aggregate says so rather than storing a zero.
/// </para>
/// </summary>
public enum ContributionKind
{
    Monetary = 1,
    Goods = 2,
    VolunteerService = 3,
    ProfessionalSkills = 4,
    Supplies = 5
}
