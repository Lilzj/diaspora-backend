using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Security;
using Diaspora.Infrastructure.Identity.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Infrastructure.Identity.Hosting;

/// <summary>
/// Ends suspensions whose end date has passed.
/// <para>
/// <b>Nothing did until 7 Nov 2026.</b> Staff could suspend a member "until 21 November", the
/// member was told that date, and on 22 November they were still suspended — until somebody
/// remembered to lift it. Now the member is reinstated to the rung they were on, and told, with
/// the note <see cref="SuspensionEndedNote"/>.
/// </para>
/// <para>
/// An indefinite suspension (no end date) is never touched: it ends when a member of staff lifts it.
/// </para>
/// </summary>
public sealed class SuspensionExpirySweep : BackgroundService
{
    /// <summary>What the member is told about why the suspension lifted.</summary>
    public const string SuspensionEndedNote = "The suspension's end date passed.";

    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<IdentityModuleOptions> _options;
    private readonly ILogger<SuspensionExpirySweep> _logger;

    public SuspensionExpirySweep(
        IServiceScopeFactory scopes,
        IOptions<IdentityModuleOptions> options,
        ILogger<SuspensionExpirySweep> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;

        if (!options.EnableSuspensionExpirySweep)
        {
            _logger.LogInformation("SuspensionExpirySweep is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.SuspensionExpirySweepMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Reinstates one batch of members whose suspension has ended. Public so the test suite can
    /// drive it without a timer.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            var users = services.GetRequiredService<IUserRepository>();
            var securityEvents = services.GetRequiredService<ISecurityEventWriter>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var clock = services.GetRequiredService<IClock>();

            var now = clock.UtcNow;
            var ended = await users.ListEndedSuspensionsAsync(now, BatchSize, cancellationToken);
            var reinstated = 0;

            foreach (var user in ended)
            {
                var reinstatement = user.Reinstate(
                    now, AccessStateChangeReason.ModerationReinstatement, SuspensionEndedNote);

                if (reinstatement.IsFailure)
                {
                    _logger.LogWarning(
                        "Could not end the suspension of {UserId}: {Error}",
                        user.Id.Value,
                        reinstatement.Error.Description);
                    continue;
                }

                users.Update(user);

                await securityEvents.WriteAsync(new SecurityEventDraft
                {
                    UserId = user.Id,
                    Type = SecurityEventType.AccountReinstated,
                    Severity = SecurityEventSeverity.Notice,
                    Description = $"Suspension ended on its date; reinstated to {user.AccessState.Name}.",
                    Metadata = new Dictionary<string, string> { ["notes"] = SuspensionEndedNote }
                }, cancellationToken);

                reinstated++;
            }

            if (reinstated > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Ended {Count} suspension(s) whose date had passed.", reinstated);
            }

            return reinstated;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // A failed pass must not stop the loop.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            _logger.LogError(exception, "A suspension expiry pass failed. The loop continues.");

            return 0;
        }
    }
}
