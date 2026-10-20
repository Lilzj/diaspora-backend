using Diaspora.Application.Identity.Abstractions;
using Diaspora.Email.Gateways;
using Diaspora.Modules.Notifications.Extensibility.Addressing;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Email;

/// <summary>
/// Registers the email adapter, replacing the placeholder sender Notifications ships with.
/// </summary>
/// <remarks>
/// Call after <c>AddNotificationsModule</c>. The replacement is last-registration-wins on
/// <see cref="IEmailSender"/>, exactly as the SMS adapter replaces its own placeholder.
/// </remarks>
public static class EmailAdapterRegistration
{
    public static IServiceCollection AddEmailAdapter(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(EmailOptions.SectionName);
        var options = section.Get<EmailOptions>() ?? new EmailOptions();

        // ── Nothing configured: leave the placeholder alone ───────────────────────────────
        //
        // A deployment with no email credentials keeps `UnconfiguredEmailSender`, which answers
        // `IsConfigured => false`. That matters: `EmailNotificationChannel` reads exactly that
        // property to decide whether the channel is available, so an unconfigured deployment
        // routes around email instead of queueing messages that will never leave. Registering a
        // credential-less gateway here would flip the channel to "available" and turn a
        // deliberate absence into a stream of failures.
        if (!options.Resend.IsConfigured)
        {
            return services;
        }

        services.AddOptions<EmailOptions>()
            .Bind(section)
            .ValidateDataAnnotations();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<EmailOptions>, EmailOptionsValidator>());

        // Declared here rather than in the platform's own contributor: the numbers are about what
        // an email is worth and how long a dispatch will wait, and neither is the platform's
        // business.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IOperationalPolicyContributor, EmailResiliencePolicyContributor>());

        // ── Add, not TryAddEnumerable ─────────────────────────────────────────────────────
        //
        // The same trap the SMS adapter documents. `TryAddEnumerable` de-duplicates on the
        // descriptor's *implementation type*, and a factory descriptor has none — the
        // implementation type reads as `IFailureClassifier` itself, which it refuses as
        // "indistinguishable from other services registered for" the same interface. It throws
        // during registration, so the host aborts before binding a port.
        //
        // There is one classifier today, but it is registered the same way the SMS ones are so
        // that adding a second provider does not require rediscovering this.
        services.AddSingleton<IFailureClassifier>(
            _ => new EmailFailureClassifier(EmailDependencies.Resend));

        // The per-attempt timeout is the policy's; this one is the transport's last resort, set
        // above it so a client-level abort cannot pre-empt the executor's own accounting.
        services.AddHttpClient(ResendEmailGateway.HttpClientName, (provider, client) =>
        {
            var current = provider.GetRequiredService<IOptionsMonitor<EmailOptions>>().CurrentValue;

            client.BaseAddress = new Uri(
                string.IsNullOrWhiteSpace(current.Resend.BaseUrl)
                    ? "https://api.resend.com/"
                    : current.Resend.BaseUrl);

            client.Timeout = TimeSpan.FromSeconds(
                current.TimeoutSeconds > 0 ? current.TimeoutSeconds + 5 : 20);
        });

        services.AddSingleton<IEmailGateway>(provider => new ResendEmailGateway(
            provider.GetRequiredService<IOptionsMonitor<EmailOptions>>(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(ResendEmailGateway.HttpClientName),
            provider.GetRequiredService<ILogger<ResendEmailGateway>>()));

        // Replaces `UnconfiguredEmailSender` on last-registration-wins. Scoped, matching the
        // placeholder's lifetime so nothing that resolves it has to change.
        services.AddScoped<IEmailSender, GatewayEmailSender>();

        // Email verification links, replacing Identity's unconfigured placeholder. Registered here
        // because only a deployment that can send email can prove a member receives it.
        services.AddScoped<IEmailVerificationSender, GatewayEmailVerificationSender>();

        // The same reasoning for moving the sign-in address: the link, the "address in use" notice
        // and the notices to the old address.
        services.AddScoped<IEmailChangeMailer, GatewayEmailChangeMailer>();

        DecorateOtpDelivery(services);

        // ── The other half of an email provider ───────────────────────────────────────────
        //
        // Notifications ships `NullRecipientAddressResolver`, which resolves nothing, because a
        // member's email address belongs to Identity and no published contract carries it. That is
        // a deliberate decision rather than a gap: it keeps one owner for the platform's most
        // sensitive contact detail.
        //
        // The consequence, until this line existed, was that every notification email failed with
        // `NoEndpoint` — transport configured, template rendered, preferences evaluated, message
        // composed, and nowhere to send it. Last-registration-wins replaces the placeholder, and
        // only here, where the deployment has already said it can send email at all.
        services.AddScoped<IRecipientAddressResolver, IdentityRecipientAddressResolver>();

        return services;
    }

    /// <summary>
    /// Puts <see cref="EmailOtpDeliveryService"/> in front of whichever OTP transport is already
    /// registered, so recovery codes go by email and everything else carries on unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A decoration rather than a replacement, which is why this cannot use last-registration-wins
    /// the way <see cref="IEmailSender"/> does. Email cannot carry a phone-verification code — the
    /// code has to arrive on the number being claimed — so the SMS transport has to survive
    /// underneath rather than be displaced.
    /// </para>
    /// <para>
    /// <b>Order matters.</b> This captures whatever is registered <em>at the moment it runs</em>,
    /// so <c>AddEmailAdapter</c> has to come after <c>AddSmsAdapter</c> in composition. Called
    /// earlier it would wrap <c>LoggingOtpDeliveryService</c>, and the SMS adapter's own
    /// registration would then replace the whole decorated chain — recovery would silently go back
    /// to SMS with nothing failing to say so.
    /// </para>
    /// </remarks>
    private static void DecorateOtpDelivery(IServiceCollection services)
    {
        var inner = services.LastOrDefault(
            descriptor => descriptor.ServiceType == typeof(IOtpDeliveryService));

        if (inner is null)
        {
            // Identity's infrastructure registers a transport before this runs, so reaching here
            // means composition changed. Refusing quietly is right: this decorator has nothing to
            // fall back to, and a recovery code for a member with no email address would have
            // nowhere to go.
            return;
        }

        // ── Guard against a second `AddEmailAdapter` ──────────────────────────────────────
        //
        // Without this the second call wraps the first call's decorator: two live gateways for
        // one send, and an SMS fallback two layers down.
        //
        // Matching on the descriptor does not work, and the reason is the same gap documented
        // for `TryAddEnumerable` above — the decorator is registered by factory, and a factory
        // descriptor carries no `ImplementationType` to compare against. So the decoration
        // leaves a marker instead, and that is what a second pass looks for.
        if (services.Any(descriptor => descriptor.ServiceType == typeof(OtpDeliveryDecorated)))
        {
            return;
        }

        services.AddScoped<IOtpDeliveryService>(provider => new EmailOtpDeliveryService(
            ResolveInner(provider, inner),
            provider.GetRequiredService<IEmailGateway>(),
            provider.GetRequiredService<IOptionsMonitor<EmailOptions>>(),
            provider.GetRequiredService<IResilienceExecutor>(),
            provider.GetRequiredService<ILocalizedMessageProvider>(),
            provider.GetRequiredService<IClock>(),
            provider.GetRequiredService<ILogger<EmailOtpDeliveryService>>()));

        services.AddSingleton(new OtpDeliveryDecorated());
    }

    /// <summary>
    /// The trace the OTP decoration leaves, so a repeated registration can recognise it.
    /// </summary>
    /// <remarks>
    /// Never resolved. It exists because the decorator's own descriptor is anonymous — see the
    /// note in <see cref="DecorateOtpDelivery"/>.
    /// </remarks>
    private sealed class OtpDeliveryDecorated;

    /// <summary>
    /// Builds the transport this decorator wraps, from the descriptor that was registered for it.
    /// </summary>
    /// <remarks>
    /// The container cannot resolve it for us: asking it for <see cref="IOtpDeliveryService"/>
    /// inside this factory would return the decorator, which is this factory, which is a stack
    /// overflow at the first recovery request rather than at startup.
    /// </remarks>
    private static IOtpDeliveryService ResolveInner(
        IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IOtpDeliveryService instance)
        {
            return instance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return (IOtpDeliveryService)descriptor.ImplementationFactory(provider);
        }

        return (IOtpDeliveryService)ActivatorUtilities.CreateInstance(
            provider, descriptor.ImplementationType!);
    }
}
