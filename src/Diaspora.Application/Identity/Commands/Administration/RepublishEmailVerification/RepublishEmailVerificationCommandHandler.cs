using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Messaging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.Commands.Administration.RepublishEmailVerification;

/// <summary>
/// Publishes the email-verified fact again. See <see cref="RepublishEmailVerificationCommand"/>.
/// </summary>
public sealed class RepublishEmailVerificationCommandHandler
    : ICommandHandler<RepublishEmailVerificationCommand, OperationAcknowledgedResponse>
{
    private readonly IUserRepository _users;
    private readonly IIntegrationEventPublisher _integrationEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RepublishEmailVerificationCommandHandler> _logger;

    public RepublishEmailVerificationCommandHandler(
        IUserRepository users,
        IIntegrationEventPublisher integrationEvents,
        IUnitOfWork unitOfWork,
        ILogger<RepublishEmailVerificationCommandHandler> logger)
    {
        _users = users;
        _integrationEvents = integrationEvents;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<OperationAcknowledgedResponse>> Handle(
        RepublishEmailVerificationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _users.GetByIdAsync(UserId.From(request.UserId), cancellationToken);

        if (user is null)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.NotFound);
        }

        // A repair tool must not be able to grant what was never proved.
        if (user.Email is null || user.Security.EmailVerifiedAt is not { } verifiedAt)
        {
            return Result.Failure<OperationAcknowledgedResponse>(UserErrors.EmailNotVerified);
        }

        await _integrationEvents.PublishAsync(
            new UserEmailVerifiedIntegrationEvent
            {
                UserId = user.Id.Value,
                EmailAddress = user.Email.Value,
                VerifiedAt = verifiedAt
            },
            cancellationToken);

        // Publishing enqueues into the outbox inside this handler's transaction; without the
        // commit nothing is sent. The phone's twin shipped without it once.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Re-announced the verified email for {UserId} at an operator's request.", user.Id.Value);

        return Result.Success(
            OperationAcknowledgedResponse.Create("The verified email was announced again."));
    }
}
