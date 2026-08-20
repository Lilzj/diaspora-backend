using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Application.Services.Snapshots;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Domain.Drafts;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Writes one auto-save snapshot and prunes the guide's history back to its ceiling.
/// <para>
/// The guide is loaded only to answer two questions — does it exist, and is it the caller's —
/// and is never mutated, so an auto-save cannot bump the revision an open editor is holding.
/// The client's base revision is still checked: a snapshot taken against a document somebody
/// else has since rewritten is insurance against the wrong draft.
/// </para>
/// <para>
/// Pruning is a storage policy rather than a domain rule, which is why it lives here and not on
/// the snapshot aggregate. It runs after every write, so the table is bounded by construction
/// rather than by a sweep that may or may not be scheduled.
/// </para>
/// </summary>
public sealed class SaveGuideDraftCommandHandler : ICommandHandler<SaveGuideDraftCommand>
{
    private readonly IGuideRepository _guides;
    private readonly IGuideDraftSnapshotRepository _snapshots;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SaveGuideDraftCommandHandler(
        IGuideRepository guides,
        IGuideDraftSnapshotRepository snapshots,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _guides = guides;
        _snapshots = snapshots;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        SaveGuideDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guideId = GuideId.From(command.GuideId);
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        if (guide is null)
        {
            return Result.Failure(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var current = guide.EnsureRevision(command.Request.BaseRevision);

        if (current.IsFailure)
        {
            return current;
        }

        var snapshot = GuideDraftSnapshot.Create(
            GuideDraftSnapshotId.From(_guids.NewSequentialGuid()),
            guideId,
            GuideSnapshotSerializer.Serialize(ToSnapshot(command.Request)),
            caller.Value,
            _clock.UtcNow);

        if (snapshot.IsFailure)
        {
            return Result.Failure(snapshot.Error);
        }

        await _snapshots.AddAsync(snapshot.Value, cancellationToken);
        await _snapshots.PruneAsync(guideId, GuideLimits.MaxDraftSnapshots, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Projects the editor's wire structure into the serializer's shape.
    /// <para>
    /// The block kind is carried across as the client sent it rather than parsed: an auto-save
    /// of a block kind this deployment has not shipped yet is still worth keeping, and the real
    /// save is where an unknown kind is refused.
    /// </para>
    /// </summary>
    private static GuideSnapshot ToSnapshot(SaveGuideDraftRequest request) => new()
    {
        Sections = request.Sections
            .Select(section => new GuideSectionSnapshot
            {
                SectionId = section.SectionId,
                Title = section.Title,
                Order = section.Order,
                Blocks = section.Blocks
                    .Select(block => new GuideBlockSnapshot
                    {
                        BlockId = block.BlockId,
                        Kind = block.Kind,
                        Order = block.Order,
                        Content = block.Content,
                        MediaId = block.MediaId,
                        Metadata = block.Metadata
                    })
                    .ToList()
            })
            .ToList()
    };
}
