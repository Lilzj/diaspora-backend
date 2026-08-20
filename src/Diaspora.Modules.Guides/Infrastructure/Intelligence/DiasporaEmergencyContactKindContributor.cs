using Diaspora.Modules.Guides.Extensibility.Intelligence;

namespace Diaspora.Modules.Guides.Infrastructure.Intelligence;

/// <summary>
/// The ten kinds of emergency contact the platform ships with.
/// <para>
/// <b>Three are marked essential and seven are not, and the line is drawn on availability rather
/// than importance.</b> Police, fire and ambulance exist in some form everywhere, so a country
/// directory missing one is almost certainly incomplete and worth flagging to an editor. A
/// domestic-violence hotline is no less important and genuinely does not exist as a separate number in
/// many countries — flagging its absence would train editors to ignore the flag, which would then not
/// catch the missing ambulance number either.
/// </para>
/// <para>
/// <b>The names here are the platform's, never the reader's.</b> A reader sees the label the country's
/// editor wrote, because the body a Francophone country calls for police is the gendarmerie. These
/// names appear in an editorial picker and nowhere else.
/// </para>
/// </summary>
public sealed class DiasporaEmergencyContactKindContributor : IEmergencyContactKindContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public int Order => 100;

    public IEnumerable<EmergencyContactKindSeed> GetKinds()
    {
        yield return new EmergencyContactKindSeed
        {
            Key = "police",
            Name = "Police",
            Icon = "👮",
            Order = 10,
            IsEssential = true
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "fire",
            Name = "Fire",
            Icon = "🚒",
            Order = 20,
            IsEssential = true
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "ambulance",
            Name = "Ambulance",
            Icon = "🚑",
            Order = 30,
            IsEssential = true
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "disaster-management",
            Name = "Disaster Management",
            Icon = "🌀",
            Order = 40
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "embassy-emergency",
            Name = "Embassy Emergency Line",
            Icon = "🏢",
            Order = 50
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "medical-hotline",
            Name = "Medical Hotline",
            Icon = "🏥",
            Order = 60
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "womens-safety",
            Name = "Women's Safety",
            Icon = "🛡️",
            Order = 70
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "child-protection",
            Name = "Child Protection",
            Icon = "🧒",
            Order = 80
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "domestic-violence",
            Name = "Domestic Violence",
            Icon = "☎️",
            Order = 90
        };

        yield return new EmergencyContactKindSeed
        {
            Key = "mental-health",
            Name = "Mental Health",
            Icon = "💚",
            Order = 100
        };
    }
}
