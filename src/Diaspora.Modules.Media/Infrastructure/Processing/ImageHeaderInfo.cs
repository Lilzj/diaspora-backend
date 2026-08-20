using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Infrastructure.Processing;

/// <summary>
/// What an image's header says about it.
/// <para>
/// <see cref="Dimensions"/> is the <em>effective</em> size — axes already swapped when the
/// orientation flag says the camera wrote a portrait photograph in landscape. Reporting the
/// stored size instead is the root of every "the layout is wrong for photos from my phone"
/// report, and the swap belongs here rather than at each of the several call sites that read
/// dimensions.
/// </para>
/// </summary>
public sealed record ImageHeaderInfo(
    MediaDimensions Dimensions,
    int? Orientation,
    bool HasEmbeddedMetadata);
