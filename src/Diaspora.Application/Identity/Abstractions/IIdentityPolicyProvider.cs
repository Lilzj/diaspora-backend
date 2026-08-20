using Diaspora.Application.Identity.Models;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Supplies the active identity policy set. Implemented over the configuration system in
/// Infrastructure; injected as a port so the Application layer stays configuration-free.
/// </summary>
public interface IIdentityPolicyProvider
{
    IdentityPolicySet Current { get; }
}
