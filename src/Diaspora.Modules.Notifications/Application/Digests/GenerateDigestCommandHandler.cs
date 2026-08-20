using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Digests;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Digests;

/// <summary>
/// The member asked for a summary now.
/// <para>
/// <c>RecordWhenEmpty</c> is false here and true from the sweep, and the difference is who is
/// waiting: somebody who pressed a button would rather be told there is nothing than handed an empty
/// page, while the sweep records the empty window so "why do I never get my daily summary" has an
/// answer.
/// </para>
/// </summary>
public sealed class GenerateDigestCommandHandler
    : ICommandHandler<GenerateDigestCommand, NotificationDigestResponse>
{
    /// <summary>A month. Past that it is an export, not a digest.</summary>
    private const int MaxWindowHours = 24 * 31;

    private readonly IDigestGenerator _generator;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public GenerateDigestCommandHandler(
        IDigestGenerator generator,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _generator = generator;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<NotificationDigestResponse>> Handle(
        GenerateDigestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NotificationDigestResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var hours = Math.Clamp(command.Request.WindowHours ?? 24, 1, MaxWindowHours);

        var digest = await _generator.GenerateAsync(
            new DigestRequest
            {
                RecipientUserId = caller.Value,
                Frequency = DigestFrequency.Manual,
                WindowStart = now.AddHours(-hours),
                WindowEnd = now,
                CategoryKey = command.Request.CategoryKey,
                RecordWhenEmpty = false
            },
            cancellationToken);

        if (digest.IsFailure)
        {
            return Result.Failure<NotificationDigestResponse>(digest.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(DigestMapper.ToResponse(digest.Value));
    }
}
