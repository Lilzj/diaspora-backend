using Diaspora.Domain.Identity.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Persistence.Identity.Repositories;

/// <summary>
/// Registers the identity repositories. Called from the identity platform module so the
/// composition root does not have to know each repository individually.
/// </summary>
public static class IdentityPersistenceRegistration
{
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserSessionRepository, UserSessionRepository>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
        services.AddScoped<IOtpChallengeRepository, OtpChallengeRepository>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();

        return services;
    }
}
