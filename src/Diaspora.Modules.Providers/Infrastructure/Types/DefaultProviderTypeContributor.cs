using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Extensibility.Types;

namespace Diaspora.Modules.Providers.Infrastructure.Types;

/// <summary>
/// The provider types every deployment offers unless it says otherwise.
/// <para>
/// <b>Every kind the brief names is here, and none of them requires a registration number.</b>
/// That is the "do not hardcode assumptions about businesses only" instruction made concrete: an
/// individual braider, a community group that meets in a front room and a mosque with no
/// charity number are all registrable out of the box. A regulated market that wants to demand
/// registration from clinics contributes its own descriptors at a higher order and overrides
/// these — which is what the seam is for.
/// </para>
/// <para>
/// <see cref="ProviderTypeDescriptor.VerificationKinds"/> is the field that keeps the product
/// honest per type. A church is verified as an organization, not as a business; showing "verify
/// your business registration" on a mosque's dashboard is how a product tells a community it was
/// not designed for them.
/// </para>
/// </summary>
public sealed class DefaultProviderTypeContributor : IProviderTypeContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public int Order => 0;

    public IEnumerable<ProviderTypeDescriptor> GetProviderTypes()
    {
        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.Individual,
            Label = "Individual",
            Description = "You offer a service in your own name.",
            Icon = "👤",

            // An individual has no company, so the wizard never asks for one. This single flag
            // is what stops a braider being shown a form designed for a limited company.
            SupportsOrganizationDetail = false,
            VerificationKinds = [ProviderVerificationKind.Identity, ProviderVerificationKind.Certification],
            DefaultDeliveryMode = ServiceDeliveryMode.Hybrid
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.Business,
            Label = "Business",
            Description = "A shop, restaurant, salon or trading business.",
            Icon = "🏪",
            VerificationKinds =
            [
                ProviderVerificationKind.Identity,
                ProviderVerificationKind.Business,
                ProviderVerificationKind.Document,
                ProviderVerificationKind.Certification
            ],
            DefaultDeliveryMode = ServiceDeliveryMode.OnSite
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.Organization,
            Label = "Organization",
            Description = "An association, NGO or charity.",
            Icon = "🏛",
            VerificationKinds =
            [
                ProviderVerificationKind.Organization,
                ProviderVerificationKind.Document
            ],
            DefaultDeliveryMode = ServiceDeliveryMode.OnSite
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.Church,
            Label = "Church, mosque or place of worship",
            Description = "A congregation with a place people gather.",
            Icon = "🕌",
            VerificationKinds = [ProviderVerificationKind.Organization],
            DefaultDeliveryMode = ServiceDeliveryMode.OnSite
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.CommunityGroup,
            Label = "Community group",
            Description = "An informal group with no legal form.",
            Icon = "🤝",

            // Deliberately no verification kinds. A hometown union that meets in somebody's
            // front room has nothing to prove and nowhere to prove it, and offering a
            // verification it can never obtain is an invitation to feel second-class.
            VerificationKinds = [],
            DefaultDeliveryMode = ServiceDeliveryMode.Hybrid
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.GovernmentService,
            Label = "Embassy or public service",
            Description = "A consulate, council office or other public body.",
            Icon = "🏢",
            VerificationKinds =
            [
                ProviderVerificationKind.Embassy,
                ProviderVerificationKind.Organization
            ],
            DefaultDeliveryMode = ServiceDeliveryMode.OnSite
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.EducationalInstitution,
            Label = "School or college",
            Description = "A school, college, university or supplementary school.",
            Icon = "🎓",
            VerificationKinds =
            [
                ProviderVerificationKind.Organization,
                ProviderVerificationKind.Document
            ],
            DefaultDeliveryMode = ServiceDeliveryMode.OnSite
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.HealthcareProvider,
            Label = "Healthcare provider",
            Description = "A clinic, surgery, pharmacy or practitioner.",
            Icon = "🩺",
            VerificationKinds =
            [
                ProviderVerificationKind.Identity,
                ProviderVerificationKind.Professional,
                ProviderVerificationKind.Certification,
                ProviderVerificationKind.Business
            ],
            DefaultDeliveryMode = ServiceDeliveryMode.OnSite
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.ProfessionalConsultant,
            Label = "Professional consultant",
            Description = "A lawyer, accountant, immigration adviser or other licensed professional.",
            Icon = "💼",
            VerificationKinds =
            [
                ProviderVerificationKind.Identity,
                ProviderVerificationKind.Professional,
                ProviderVerificationKind.Certification
            ],
            DefaultDeliveryMode = ServiceDeliveryMode.Hybrid
        };

        yield return new ProviderTypeDescriptor
        {
            Type = ProviderType.Other,
            Label = "Something else",
            Description = "None of the above.",
            Icon = "📌",
            VerificationKinds = [ProviderVerificationKind.Identity],
            DefaultDeliveryMode = ServiceDeliveryMode.Hybrid
        };
    }
}
