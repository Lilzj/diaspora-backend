using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Extensibility.Intelligence;

namespace Diaspora.Modules.Guides.Infrastructure.Intelligence;

/// <summary>
/// The twenty kinds of official notice the platform ships with.
/// <para>
/// <b>The default severity on each is the interesting column, and it is a judgement rather than a
/// taxonomy.</b> An emergency alert is critical because somebody may have to act within the hour. A
/// visa advisory is high because it changes what somebody does this month and there is a deadline
/// behind it. A public holiday is informational because missing it costs a wasted trip to a closed
/// office — annoying, and not worth waking anybody for. Getting these wrong in either direction is
/// how a notification system trains people to ignore it.
/// </para>
/// <para>
/// <b>Three categories carry an alert family, and only three.</b> Emergency alerts, safety advisories
/// and natural disaster notices publish an emergency contract on top of the general one; government
/// announcements and election information publish a government one. Everything else publishes once. A
/// deployment that disagrees edits the category rather than this file — that is what the seam is for.
/// </para>
/// <para>
/// Shipped at order 100, so a deployment registering below it can redefine any of these before the
/// seeder sees them.
/// </para>
/// </summary>
public sealed class DiasporaNoticeCategoryContributor : IOfficialNoticeCategoryContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public int Order => 100;

    public IEnumerable<NoticeCategorySeed> GetCategories()
    {
        // ── The ones somebody may have to act on today ──────────────────────────────────────

        yield return new NoticeCategorySeed
        {
            Key = "emergency-alerts",
            Name = "Emergency Alerts",
            Description = "Something happening now that you may need to act on.",
            Icon = "🚨",
            Order = 10,
            DefaultSeverity = NoticeSeverity.Critical,
            AlertFamily = NoticeAlertFamily.Emergency
        };

        yield return new NoticeCategorySeed
        {
            Key = "safety-advisories",
            Name = "Safety Advisories",
            Description = "Areas, routes or situations to avoid.",
            Icon = "🛡️",
            Order = 20,
            DefaultSeverity = NoticeSeverity.High,
            AlertFamily = NoticeAlertFamily.Emergency
        };

        yield return new NoticeCategorySeed
        {
            Key = "natural-disaster-notices",
            Name = "Natural Disaster Notices",
            Description = "Cyclones, floods, earthquakes and the official response to them.",
            Icon = "🌀",
            Order = 30,
            DefaultSeverity = NoticeSeverity.Critical,
            AlertFamily = NoticeAlertFamily.Emergency
        };

        yield return new NoticeCategorySeed
        {
            Key = "public-health-advisories",
            Name = "Public Health Advisories",
            Description = "Outbreaks, vaccination requirements and health guidance.",
            Icon = "🏥",
            Order = 40,
            DefaultSeverity = NoticeSeverity.High,
            AlertFamily = NoticeAlertFamily.Emergency
        };

        // ── Movement and status ─────────────────────────────────────────────────────────────

        yield return new NoticeCategorySeed
        {
            Key = "visa-advisories",
            Name = "Visa Advisories",
            Description = "Changes to visa requirements, categories and eligibility.",
            Icon = "🛂",
            Order = 50,
            DefaultSeverity = NoticeSeverity.High
        };

        yield return new NoticeCategorySeed
        {
            Key = "immigration-updates",
            Name = "Immigration Updates",
            Description = "Residence, work permits and status changes.",
            Icon = "📋",
            Order = 60,
            DefaultSeverity = NoticeSeverity.High
        };

        yield return new NoticeCategorySeed
        {
            Key = "border-changes",
            Name = "Border Changes",
            Description = "Crossings opening, closing or changing their rules.",
            Icon = "🚧",
            Order = 70,
            DefaultSeverity = NoticeSeverity.High
        };

        yield return new NoticeCategorySeed
        {
            Key = "travel-restrictions",
            Name = "Travel Restrictions",
            Description = "Who may travel, from where, and on what conditions.",
            Icon = "✈️",
            Order = 80,
            DefaultSeverity = NoticeSeverity.High
        };

        yield return new NoticeCategorySeed
        {
            Key = "passport-services",
            Name = "Passport Services",
            Description = "Applications, renewals, collection and processing times.",
            Icon = "📕",
            Order = 90,
            DefaultSeverity = NoticeSeverity.Medium
        };

        yield return new NoticeCategorySeed
        {
            Key = "consular-services",
            Name = "Consular Services",
            Description = "Legalisation, attestation, notarial acts and civil registration.",
            Icon = "🗂️",
            Order = 100,
            DefaultSeverity = NoticeSeverity.Medium
        };

        // ── The state speaking ──────────────────────────────────────────────────────────────

        yield return new NoticeCategorySeed
        {
            Key = "government-announcements",
            Name = "Government Announcements",
            Description = "Formal statements from national government.",
            Icon = "🏛️",
            Order = 110,
            DefaultSeverity = NoticeSeverity.Medium,
            AlertFamily = NoticeAlertFamily.GovernmentAnnouncement
        };

        yield return new NoticeCategorySeed
        {
            Key = "embassy-notices",
            Name = "Embassy Notices",
            Description = "Announcements from missions serving your community.",
            Icon = "🏢",
            Order = 120,
            DefaultSeverity = NoticeSeverity.Medium
        };

        yield return new NoticeCategorySeed
        {
            Key = "election-information",
            Name = "Election Information",
            Description = "Registration, voting abroad, dates and results.",
            Icon = "🗳️",
            Order = 130,
            DefaultSeverity = NoticeSeverity.Medium,
            AlertFamily = NoticeAlertFamily.GovernmentAnnouncement
        };

        yield return new NoticeCategorySeed
        {
            Key = "legal-changes",
            Name = "Legal Changes",
            Description = "New or amended law that affects residents and nationals abroad.",
            Icon = "⚖️",
            Order = 140,
            DefaultSeverity = NoticeSeverity.Medium
        };

        yield return new NoticeCategorySeed
        {
            Key = "tax-changes",
            Name = "Tax Changes",
            Description = "Rates, filing deadlines and obligations for people abroad.",
            Icon = "💰",
            Order = 150,
            DefaultSeverity = NoticeSeverity.Medium
        };

        yield return new NoticeCategorySeed
        {
            Key = "employment-policy",
            Name = "Employment Policy",
            Description = "Work permits, labour law and employer obligations.",
            Icon = "💼",
            Order = 160,
            DefaultSeverity = NoticeSeverity.Medium
        };

        yield return new NoticeCategorySeed
        {
            Key = "education-announcements",
            Name = "Education Announcements",
            Description = "Admissions, recognition of qualifications and school calendars.",
            Icon = "🎓",
            Order = 170,
            DefaultSeverity = NoticeSeverity.Low
        };

        // ── Planning and community ──────────────────────────────────────────────────────────

        yield return new NoticeCategorySeed
        {
            Key = "public-holidays",
            Name = "Public Holidays",
            Description = "Days the offices are shut, and what that means for an appointment.",
            Icon = "📅",
            Order = 180,
            DefaultSeverity = NoticeSeverity.Informational
        };

        yield return new NoticeCategorySeed
        {
            Key = "official-events",
            Name = "Official Events",
            Description = "Outreach days, mobile consulates and open sessions.",
            Icon = "📣",
            Order = 190,
            DefaultSeverity = NoticeSeverity.Low
        };

        yield return new NoticeCategorySeed
        {
            Key = "community-bulletins",
            Name = "Community Bulletins",
            Description = "Official information addressed to the diaspora community.",
            Icon = "📰",
            Order = 200,
            DefaultSeverity = NoticeSeverity.Informational
        };
    }
}
