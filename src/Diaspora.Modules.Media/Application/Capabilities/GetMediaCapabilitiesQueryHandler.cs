using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Policies;
using Diaspora.Modules.Media.Extensibility.Processing;
using Diaspora.Modules.Media.Options;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Application.Capabilities;

/// <summary>
/// Reports the deployment's real limits and real capabilities.
/// <para>
/// Composed from what is registered rather than from a written list, so it cannot drift. The
/// variant ladder is empty on a deployment with no image codec — which is the honest answer,
/// and one a hand-maintained document would get wrong within a release.
/// </para>
/// </summary>
public sealed class GetMediaCapabilitiesQueryHandler
    : IQueryHandler<GetMediaCapabilitiesQuery, MediaCapabilitiesResponse>
{
    private readonly MediaProcessorRegistry _processors;
    private readonly MediaUploadOptions _upload;
    private readonly MediaSecurityOptions _security;

    public GetMediaCapabilitiesQueryHandler(
        MediaProcessorRegistry processors,
        IOptions<MediaUploadOptions> upload,
        IOptions<MediaSecurityOptions> security)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(security);

        _processors = processors;
        _upload = upload.Value;
        _security = security.Value;
    }

    public Task<Result<MediaCapabilitiesResponse>> Handle(
        GetMediaCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        var kinds = new[]
        {
            MediaKind.Image, MediaKind.Video, MediaKind.Audio,
            MediaKind.Document, MediaKind.Archive, MediaKind.VerificationDocument
        };

        var response = new MediaCapabilitiesResponse
        {
            MaxUploadBytes = kinds.ToDictionary(
                MediaWireNames.ToWire,
                kind => MediaLimits.MaxSizeFor(kind).Bytes),

            DirectUploadCeilingBytes =
                (long)_upload.DirectUploadCeilingMegabytes * ByteSizeConstants.BytesPerMegabyte,

            ChunkSizeBytes =
                (long)_upload.ChunkSizeMegabytes * ByteSizeConstants.BytesPerMegabyte,

            SessionLifetimeHours = _upload.SessionLifetimeHours,
            MaxConcurrentUploads = _upload.MaxConcurrentSessionsPerUser,

            AcceptedContentTypes = kinds.ToDictionary(
                MediaWireNames.ToWire,
                MediaTypeCatalog.AcceptedContentTypesFor),

            // Empty when no codec is registered. That is the point: a client can see it and
            // stop offering a "choose a size" control that will never work.
            AvailableVariants = AvailableVariants(),

            SupportedOperations = kinds.ToDictionary(
                MediaWireNames.ToWire,
                kind => (IReadOnlyList<string>)_processors
                    .SupportedOperationsFor(kind)
                    .Select(MediaWireNames.ToWire)
                    .ToList()),

            MalwareScanningEnabled = _processors.HasScanner,

            // True on every deployment: resumption is Media's own behaviour rather than a
            // provider capability.
            ResumableUploads = true,

            SignedUrlLifetimeSeconds = _security.SignedUrlMinutes * 60
        };

        return Task.FromResult(Result.Success(response));
    }

    private List<string> AvailableVariants()
    {
        var variants = new List<string>();

        foreach (var purpose in MediaLimits.LadderFor(MediaKind.Image))
        {
            var operation = purpose is MediaVariantPurpose.Preview
                ? ProcessingOperation.GeneratePreview
                : ProcessingOperation.GenerateThumbnails;

            if (_processors.Supports(MediaKind.Image, operation))
            {
                variants.Add(MediaWireNames.ToWire(purpose));
            }
        }

        if (_processors.Supports(MediaKind.Video, ProcessingOperation.TranscodeVideo))
        {
            variants.Add(MediaWireNames.ToWire(MediaVariantPurpose.Transcoded));
        }

        if (_processors.Supports(MediaKind.Video, ProcessingOperation.ExtractPosterFrame))
        {
            variants.Add(MediaWireNames.ToWire(MediaVariantPurpose.Poster));
        }

        return variants;
    }
}
