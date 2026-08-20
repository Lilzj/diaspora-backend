using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Capabilities;

/// <summary>
/// What this deployment can actually do with media.
/// <para>
/// The endpoint that lets a client stop hardcoding limits it cannot know. Whether videos
/// transcode, whether uploads are scanned, how large a file may be and how large a chunk
/// should be are deployment facts, and a client that assumes them is a client that lets a
/// member upload four hundred megabytes before finding out.
/// </para>
/// </summary>
public sealed record GetMediaCapabilitiesQuery : IQuery<MediaCapabilitiesResponse>;
