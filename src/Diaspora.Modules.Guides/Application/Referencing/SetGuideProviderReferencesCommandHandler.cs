using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Referencing;

/// <summary>
/// Reconciles the guide's provider references against the submitted list.
/// <para>
/// <b>Every new identifier is checked against B7 first, and that check does not fail soft.</b>
/// Reading provider cards degrades to bare references when the directory is unavailable, because
/// a knowledge page taken down by a directory outage would be a dependency inverted — but
/// <em>writing</em> a reference nobody validated is how a guide comes to recommend a business
/// that never existed. An outage refuses the save and the author retries.
/// </para>
/// <para>
/// Identifiers already on the guide are not re-checked: they were validated when they were
/// added, and a provider that has since been removed from the directory is a stale reference
/// rather than a bad edit.
/// </para>
/// </summary>
public sealed class SetGuideProviderReferencesCommandHandler
    : ICommandHandler<SetGuideProviderReferencesCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideProviderResolver _providers;

    public SetGuideProviderReferencesCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideProviderResolver providers)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _providers = providers;
    }

    public async Task<Result<GuideResponse>> Handle(
        SetGuideProviderReferencesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        var existing = guide.ProviderReferences.Select(reference => reference.ProviderId).ToList();
        var requested = command.Request.References;

        foreach (var reference in requested)
        {
            if (existing.Contains(reference.ProviderId))
            {
                continue;
            }

            if (!await _providers.ExistsAsync(reference.ProviderId, cancellationToken))
            {
                return Result.Failure<GuideResponse>(GuideErrors.ProviderReferenceInvalid);
            }
        }

        var keep = requested.Select(reference => reference.ProviderId).ToHashSet();

        foreach (var providerId in existing.Where(providerId => !keep.Contains(providerId)))
        {
            var removed = guide.RemoveProviderReference(providerId);

            if (removed.IsFailure)
            {
                return Result.Failure<GuideResponse>(removed.Error);
            }
        }

        foreach (var reference in requested)
        {
            var added = guide.AddProviderReference(
                reference.ProviderId, reference.Role, reference.Note);

            if (added.IsFailure)
            {
                return Result.Failure<GuideResponse>(added.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var summaries = await _providers.ResolveAsync(
            guide.ProviderReferences.Select(reference => reference.ProviderId).ToList(),
            cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, summaries, forAuthor: true));
    }
}
