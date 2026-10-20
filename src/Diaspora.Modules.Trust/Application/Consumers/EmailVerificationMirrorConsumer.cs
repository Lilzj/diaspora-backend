using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Trust.Application.Consumers;

/// <summary>
/// Mirrors Identity's email verification into Trust, exactly as
/// <see cref="PhoneVerificationMirrorConsumer"/> mirrors the phone.
/// <para>
/// <b>Email verification shipped on 5 Oct 2026 and Trust never heard about it.</b> Identity set
/// <c>EmailVerifiedAt</c> and told nobody, so the trust centre's Email row stayed undone for a
/// member who had pressed the button in the email, and <c>EmailVerificationHandler</c> (no
/// document, automatic approval, built for exactly this) had nothing to trigger it.
/// </para>
/// <para>
/// <b>Submits rather than grants</b>, for the reason the phone mirror does: the grant, the badge,
/// the reputation award (<c>EmailVerified</c>, the handler's <c>ApprovalAward</c>) and the trust
/// journey then happen the way they do for every other verification, and are paid once.
/// </para>
/// <para>
/// <b>Idempotent by a derived correlation identifier</b>, not by looking for an open case: the
/// type auto-approves, so a redelivered message finds no open case. The same identifier makes the
/// operator's re-announcement (<c>republish-email-verification</c>) free for an account that is
/// already mirrored.
/// </para>
/// </summary>
public sealed class EmailVerificationMirrorConsumer
    : IntegrationEventHandler<UserEmailVerifiedIntegrationEvent>
{
    private readonly VerificationSubmissionCoordinator _submissions;
    private readonly ILogger<EmailVerificationMirrorConsumer> _logger;

    public EmailVerificationMirrorConsumer(
        VerificationSubmissionCoordinator submissions,
        ILogger<EmailVerificationMirrorConsumer> logger)
    {
        _submissions = submissions;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        UserEmailVerifiedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var input = new VerificationSubmissionInput
        {
            UserId = UserId.From(integrationEvent.UserId),
            Type = VerificationType.Email,
            CorrelationId = MirrorCorrelationFor(integrationEvent.UserId),
            RequestedBy = "identity-email-mirror"
        };

        var existing = await _submissions.FindDuplicateAsync(input, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        var submitted = await _submissions.SubmitAsync(input, cancellationToken);

        if (submitted.IsFailure)
        {
            _logger.LogWarning(
                "Mirroring the verified email for {UserId} into Trust failed: {Code} {Description}",
                integrationEvent.UserId,
                submitted.Error.Code,
                submitted.Error.Description);
        }
    }

    /// <summary>
    /// A stable identifier for "this member's email mirror", derived rather than stored, and
    /// namespaced apart from the phone mirror's so the two never collide.
    /// </summary>
    internal static Guid MirrorCorrelationFor(Guid userId)
    {
        var seed = System.Text.Encoding.UTF8.GetBytes("trust:identity-email-mirror:" + userId);
        var hash = System.Security.Cryptography.SHA256.HashData(seed);

        return new Guid(hash.AsSpan(0, 16));
    }
}
