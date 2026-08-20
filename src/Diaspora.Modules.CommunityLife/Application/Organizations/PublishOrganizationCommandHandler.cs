using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// Makes an organization discoverable.
/// <para>
/// <b>Submitting for review and publishing are the same endpoint because they are the same
/// intention.</b> An administrator saying "this is ready" should not have to know whether their
/// deployment moderates organizations before they appear; the flag decides which of the two the
/// aggregate performs, and a deployment that turns review on later does not break the client.
/// </para>
/// </summary>
public sealed class PublishOrganizationCommandHandler
    : ICommandHandler<PublishOrganizationCommand, OrganizationResponse>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly OrganizationReadContext _read;
    private readonly IClock _clock;

    public PublishOrganizationCommandHandler(
        IOrganizationRepository organizations,
        IOrganizationAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        OrganizationReadContext read,
        IClock clock)
    {
        _organizations = organizations;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<OrganizationResponse>> Handle(
        PublishOrganizationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var organization = await _organizations.GetAsync(
            OrganizationId.From(command.OrganizationId), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireOrganizationWriteAsync(
            organization, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(caller.Error);
        }

        var now = _clock.UtcNow;

        var moved = command.Request.SubmitForReview
            ? organization.Submit(now, caller.Value)
            : organization.Publish(now, caller.Value);

        if (moved.IsFailure)
        {
            return Result.Failure<OrganizationResponse>(moved.Error);
        }

        if (!command.Request.SubmitForReview)
        {
            await RecordPublicationAsync(organization, now, cancellationToken);
        }

        _organizations.Update(organization);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (!command.Request.SubmitForReview)
        {
            CommunityLifeTelemetry.OrganizationsPublished.Add(
                1,
                new KeyValuePair<string, object?>("type", organization.OrganizationTypeKey),
                new KeyValuePair<string, object?>(
                    "country", organization.Location.CountryCode));
        }

        return Result.Success(await _read.ToResponseAsync(organization, cancellationToken));
    }

    private async Task RecordPublicationAsync(
        Organization organization, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var analytics = await _analytics.FindByOrganizationAsync(
            organization.Id, cancellationToken);

        analytics?.RecordPublication(now);
    }
}
