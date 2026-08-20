using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Extensibility.Types;

/// <summary>
/// How a deployment decides which kinds of provider it accepts, and what each one requires.
/// <para>
/// <b>The seam that stops <see cref="ProviderType"/> being a policy decision baked into an
/// enum.</b> The enum names what the platform can model; this decides what a given deployment
/// offers and what it demands. A country pilot might not want
/// <see cref="ProviderType.GovernmentService"/> until it has a relationship with the embassies;
/// a regulated market might require a registration number from every
/// <see cref="ProviderType.HealthcareProvider"/>. Neither is a code change to the aggregate.
/// </para>
/// <para>
/// It is also where "future provider types" from the brief actually lands: a module introducing
/// one adds the enum member (additive) and a descriptor here (contributed), and no existing
/// call site changes.
/// </para>
/// </summary>
public interface IProviderTypeContributor
{
    /// <summary>The module contributing.</summary>
    string Module { get; }

    /// <summary>Lower runs first; a later contributor's descriptor for a type wins.</summary>
    int Order => 100;

    /// <summary>The provider types this module makes available, with their requirements.</summary>
    IEnumerable<ProviderTypeDescriptor> GetProviderTypes();
}
