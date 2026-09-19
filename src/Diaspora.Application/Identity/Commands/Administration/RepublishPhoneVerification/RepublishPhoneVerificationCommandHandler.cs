using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Administration.RepublishPhoneVerification;

/// <summary>
/// Publishes the phone-verified fact again, for a member who was verified before anything
/// published it. See <see cref="RepublishPhoneVerificationCommand"/> for why that state exists.
/// </summary>
public sealed class RepublishPhoneVerificationCommandHandler
    : ICommandHandler<RepublishPhoneVerificationCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly IIntegrationEventPublisher _integrationEvents;
    private readonly ILogger<RepublishPhoneVerificationCommandHandler> _logger;

    public RepublishPhoneVerificationCommandHandler(
        IUserRepository users,
        IIntegrationEventPublisher integrationEvents,
        ILogger<RepublishPhoneVerificationCommandHandler> logger)
    {
        _users = users;
        _integrationEvents = integrationEvents;
        _logger = logger;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        RepublishPhoneVerificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _users.GetByIdAsync(UserId.From(request.UserId), cancellationToken);

        if (user is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.NotFound);
        }

        // Refused rather than quietly published. Announcing a verification that never happened
        // would grant the member a phone they have not proved, which is the one thing a repair
        // tool must not be able to do.
        if (!user.IsPhoneVerified)
        {
            return Result.Failure<OperationAcknowledgedResponse>(
                UserErrors.PhoneNotVerified);
        }

        await _integrationEvents.PublishAsync(
            new UserPhoneVerifiedIntegrationEvent
            {
                UserId = user.Id.Value,
                PhoneNumber = user.PhoneNumber.Value,
                VerifiedAt = user.Security.PhoneVerifiedAt!.Value
            },
            cancellationToken);

        _logger.LogInformation(
            "Re-announced the verified phone for {UserId} at an operator's request.", user.Id.Value);

        return Result.Success(
            OperationAcknowledgedResponse.Create("The verified phone was announced again."));
    }
}
