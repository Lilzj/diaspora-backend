using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Uploads;

namespace Diaspora.Modules.Media.Application.Mapping;

/// <summary>Projects upload sessions onto their wire contract.</summary>
public sealed class UploadMapper
{
    private readonly MediaUrlFactory _urls;
    private readonly MediaMapper _media;

    public UploadMapper(MediaUrlFactory urls, MediaMapper media)
    {
        _urls = urls;
        _media = media;
    }

    public UploadSessionResponse ToResponse(UploadSession session, MediaAsset? asset = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new UploadSessionResponse
        {
            SessionId = session.Id.Value,
            Status = MediaWireNames.ToWire(session.Status),
            Protocol = MediaWireNames.ToWire(session.Protocol),
            Kind = MediaWireNames.ToWire(session.Kind),
            FileName = session.FileName.Original,
            ContentType = session.DeclaredContentType.Value,
            SizeBytes = session.DeclaredSize.Bytes,
            TotalChunks = session.TotalChunks,
            ChunkSizeBytes = session.ChunkSize.Bytes,
            ReceivedChunks = session.ReceivedChunkNumbers,
            ProgressPercent = session.ProgressPercent,
            ExpiresAt = session.ExpiresAt,

            // Null once the session closes, so a client cannot keep POSTing at a session that
            // will refuse everything. The absence is the signal.
            UploadUrl = session.IsOpen ? _urls.ForUploadSession(session.Id.Value) : null,

            Media = asset is null ? null : _media.ToResponse(asset),
            FailureReason = session.FailureReason
        };
    }
}
