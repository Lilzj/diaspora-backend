namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// What a derived rendition is for.
/// <para>
/// A closed set rather than free-form width/height pairs, and that is the load-bearing
/// decision in the whole variant design. Arbitrary sizes on demand mean an attacker can mint
/// unbounded renditions of a 15 MB photo with a query string; a fixed ladder means the cache
/// key space is finite, every variant is generated once, and a CDN in front of the API has
/// something to cache.
/// </para>
/// <para>
/// The ladder was chosen from what the client actually asks for. Every mock URL in the app
/// carries <c>?w=600</c> or <c>?w=120</c>; the avatar component renders at 50 px and the
/// picker crops covers to 16:9.
/// </para>
/// </summary>
public enum MediaVariantPurpose
{
    /// <summary>The bytes exactly as uploaded. Always present; never regenerated.</summary>
    Original = 0,

    /// <summary>Square, 96 px. Avatars and list rows — the client's <c>?w=120</c>.</summary>
    Thumbnail = 1,

    /// <summary>Longest edge 320 px. Grid tiles and gallery strips.</summary>
    Small = 2,

    /// <summary>Longest edge 600 px. The client's <c>?w=600</c> — cards and covers.</summary>
    Medium = 3,

    /// <summary>Longest edge 1280 px. Full-screen viewing.</summary>
    Large = 4,

    /// <summary>A tiny, heavily blurred placeholder shown while the real image loads.</summary>
    Preview = 5,

    /// <summary>A still lifted from a video, used wherever a video needs a poster frame.</summary>
    Poster = 6,

    /// <summary>A web-safe transcode of a video or audio file.</summary>
    Transcoded = 7,

    /// <summary>Extracted text — OCR output for a document or a scanned page.</summary>
    ExtractedText = 8
}
