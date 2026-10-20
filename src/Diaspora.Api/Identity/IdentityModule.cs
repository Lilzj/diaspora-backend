using Diaspora.Api.Identity.Context;
using Diaspora.Application.Identity;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Infrastructure.Identity;
using Diaspora.Persistence.Identity.Repositories;
using Diaspora.Platform.Authorization;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.Api.Identity;

/// <summary>
/// Registers the Identity, Authentication and Progressive Access module.
/// <para>
/// Discovered by the platform kernel rather than named in <c>Program.cs</c>. It lives in
/// the API project because it is the only place that can see every layer at once, and
/// because the HTTP-bound adapters below have nowhere else to go without dragging ASP.NET
/// Core into Infrastructure.
/// </para>
/// <para>
/// Five of these registrations replace platform kernel defaults. B1 shipped
/// <c>DefaultCurrentUser</c>, <c>DefaultRequestContext</c>, <c>DefaultPermissionService</c>
/// and <c>DefaultFeatureGateService</c> — placeholders that reported every caller as
/// anonymous and every permission as granted. B2.6 adds
/// <c>IAuthorizationSubjectAccessor</c>, whose kernel default denies everything rather than
/// granting it. <c>Replace</c> rather than <c>Add</c> so each substitution is deliberate and
/// visible.
/// </para>
/// <para>
/// What is <em>not</em> here matters as much as what is. Identity's permissions, feature
/// gates and authorisation policies are no longer registered from this method — they are
/// declared by contributors the kernel discovers, which is what lets Trust, Marketplace and
/// Messaging declare theirs without editing this file.
/// </para>
/// </summary>
public sealed class IdentityModule : IPlatformModule
{
    public string Name => IdentityModuleMetadata.Name;

    public string Version => IdentityModuleMetadata.Version;

    /// <summary>
    /// Registers before feature modules. Everything downstream depends on the permission
    /// and feature-gate services this module supplies.
    /// </summary>
    public int Order => 1;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddHttpContextAccessor();

        // ─── HTTP-bound platform contracts ──────────────────────────────
        services.Replace(ServiceDescriptor.Scoped<ICurrentUser, HttpContextCurrentUser>());
        services.Replace(ServiceDescriptor.Scoped<IRequestContext, HttpContextRequestContext>());
        services.Replace(
            ServiceDescriptor.Scoped<IAuthorizationSubjectAccessor, ClaimsAuthorizationSubjectAccessor>());
        services.AddScoped<ICurrentIdentityContext, HttpContextCurrentIdentity>();

        // The members row of the platform overview. See the type's own summary.
        services.AddScoped<Diaspora.Contracts.Content.V1.IPlatformInventorySource, Audiences.MemberInventorySource>();

        // ─── Module layers ──────────────────────────────────────────────
        services.AddIdentityPersistence();
        services.AddIdentityInfrastructure(configuration);
    }
}
