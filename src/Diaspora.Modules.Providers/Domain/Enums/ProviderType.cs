namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// What kind of thing the provider is.
/// <para>
/// <b>The single most load-bearing enum in the module</b>, and the one the brief is most
/// explicit about: "Do not hardcode assumptions about businesses only." A church, an embassy,
/// a student association and a freelance immigration consultant are all providers here, and
/// none of them has a business registration number, a price list or opening hours in the sense
/// a restaurant does.
/// </para>
/// <para>
/// The consequence runs through the whole aggregate: every organizational field is optional,
/// operating hours are optional, pricing is optional, and the only thing every provider must
/// have is a name, a type, a category and an owner. A model that required a price would have
/// no way to represent a mosque.
/// </para>
/// <para>
/// An enum with a reserved <see cref="Other"/> rather than a free-form string, for the reason
/// B5 gives for its own owner type and B6 for its content type: a string lets any module invent
/// a value that then appears in indexes, search facets and audit records with no way to
/// enumerate what exists. Adding a provider kind adds a member here — additive, and the only
/// line B7 expects a later phase to touch.
/// </para>
/// <para>
/// Note what this enum is <em>not</em>: it is not a category. A restaurant and a barber are
/// both <see cref="Business"/>; what distinguishes them is their
/// <see cref="Categories.ServiceCategory"/>. Type answers "what legal and social kind of entity
/// is this", category answers "what does it do", and conflating the two is what forced the
/// frontend to keep two disjoint category vocabularies.
/// </para>
/// </summary>
public enum ProviderType
{
    /// <summary>A person offering a service in their own name. A braider, a tutor, a driver.</summary>
    Individual = 1,

    /// <summary>A trading business, registered or otherwise. The frontend's whole mock set.</summary>
    Business = 2,

    /// <summary>A formally constituted body: an association, an NGO, a charity.</summary>
    Organization = 3,

    /// <summary>A church, mosque, temple or other place of worship.</summary>
    Church = 4,

    /// <summary>An informal community group with no legal form. A WhatsApp group with a name.</summary>
    CommunityGroup = 5,

    /// <summary>An embassy, consulate, council office or other public service.</summary>
    GovernmentService = 6,

    /// <summary>A school, college, university, or a supplementary language school.</summary>
    EducationalInstitution = 7,

    /// <summary>A clinic, surgery, pharmacy or practitioner.</summary>
    HealthcareProvider = 8,

    /// <summary>A licensed professional: a lawyer, accountant, immigration adviser.</summary>
    ProfessionalConsultant = 9,

    /// <summary>A provider kind a later module introduced before this enum caught up.</summary>
    Other = 99
}
