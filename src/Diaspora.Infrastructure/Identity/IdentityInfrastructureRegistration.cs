using Diaspora.Infrastructure.Identity.EmailVerification;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Access;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Services;
using Diaspora.Infrastructure.Identity.Auditing;
using Diaspora.Infrastructure.Identity.Authorization;
using Diaspora.Infrastructure.Identity.Events;
using Diaspora.Infrastructure.Identity.FeatureGates;
using Diaspora.Infrastructure.Identity.Localization;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Infrastructure.Identity.Otp;
using Diaspora.Infrastructure.Identity.Passwords;
using Diaspora.Infrastructure.Identity.Policies;
using Diaspora.Infrastructure.Identity.Security;
using Diaspora.Infrastructure.Identity.Tokens;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.Infrastructure.Identity;

/// <summary>
/// Registers the identity module's application services and infrastructure adapters.
/// <para>
/// Two of these registrations deliberately replace platform kernel defaults. B1 shipped
/// <c>DefaultPermissionService</c> and <c>DefaultFeatureGateService</c>, both of which
/// return true for everything — placeholders that were correct while no identity existed
/// and are dangerous once one does. <c>Replace</c> rather than <c>Add</c> makes the
/// substitution explicit and fails loudly if the shape of the contract ever changes.
/// </para>
/// </summary>
public static class IdentityInfrastructureRegistration
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);

        // ─── Application services (pure orchestration, no infrastructure) ───
        services.AddSingleton<IFeatureAccessResolver, FeatureAccessResolver>();
        services.AddSingleton<IPermissionResolver, PermissionResolver>();
        services.AddSingleton<IAccessGateway, AccessGateway>();

        services.AddScoped<IOtpChallengeService, OtpChallengeService>();
        services.AddScoped<ITokenIssuanceService, TokenIssuanceService>();
        services.AddScoped<ISessionRevocationService, SessionRevocationService>();
        services.AddScoped<IDeviceRegistrationService, DeviceRegistrationService>();

        // ─── Infrastructure adapters ────────────────────────────────────────
        services.AddSingleton<IPasswordHasher, PbkdfPasswordHasher>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<IRefreshTokenIssuer, RefreshTokenIssuer>();
        services.AddSingleton<IServiceTokenIssuer, ServiceTokenIssuer>();
        services.AddSingleton<IOtpCodeGenerator, SecureOtpCodeGenerator>();
        services.AddSingleton<IIdentityPolicyProvider, IdentityPolicyProvider>();

        services.AddScoped<IPasswordPolicy, ConfigurablePasswordPolicy>();
        services.AddScoped<IOtpDeliveryService, LoggingOtpDeliveryService>();

        // Email verification links: signed with a key derived from the JWT key, so nothing new
        // to configure. The sender is a placeholder the email adapter replaces when Resend is set.
        services.AddSingleton<IEmailVerificationLinks, EmailVerificationLinks>();
        services.AddScoped<IEmailVerificationSender, UnconfiguredEmailVerificationSender>();
        services.AddScoped<ILocalizedMessageProvider, LocalizedMessageProvider>();
        services.AddScoped<ISecurityEventWriter, SecurityEventWriter>();
        services.AddScoped<IIdentityAuditWriter, IdentityAuditWriter>();
        services.AddScoped<IIntegrationEventPublisher, MediatRIntegrationEventPublisher>();

        // ─── Platform kernel contracts now backed by real identity ──────────
        services.Replace(ServiceDescriptor.Scoped<IPermissionService, ClaimsPermissionService>());
        services.Replace(ServiceDescriptor.Scoped<IFeatureGateService, AccessStateFeatureGateService>());

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<OtpOptions>()
            .Bind(configuration.GetSection(OtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PasswordPolicyOptions>()
            .Bind(configuration.GetSection(PasswordPolicyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LockoutOptions>()
            .Bind(configuration.GetSection(LockoutOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<IdentityModuleOptions>()
            .Bind(configuration.GetSection(IdentityModuleOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Resolves the Development signing key into JwtOptions before anything reads it.
        // Registered as a post-configure so it runs ahead of validation and ahead of every
        // consumer, which is what makes the resolved key single-source-of-truth: the access
        // token issuer, the service token issuer, the bearer validator and the OTP code
        // generator all read the same value. A second fallback elsewhere would recreate the
        // defect where a token could be validated but never issued.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Options.IPostConfigureOptions<JwtOptions>,
                JwtDevelopmentSigningKeyResolver>());

        // Cross-cutting checks the data annotations cannot express — a weak signing key,
        // or OTP codes being logged outside Development.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Options.IValidateOptions<JwtOptions>,
                JwtOptionsValidator>());

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<Microsoft.Extensions.Options.IValidateOptions<IdentityModuleOptions>,
                IdentityModuleOptionsValidator>());
    }
}
