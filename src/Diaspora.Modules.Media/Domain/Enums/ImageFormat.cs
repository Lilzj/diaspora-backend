namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// The image container formats the module recognises by magic bytes.
/// <para>
/// <see cref="Heic"/> earns its place: <c>expo-image-picker</c> on iOS returns HEIC unless the
/// caller opts out, and no screen in the app opts out. A platform that rejects HEIC rejects
/// the default output of every iPhone camera.
/// </para>
/// </summary>
public enum ImageFormat
{
    Unknown = 0,
    Jpeg = 1,
    Png = 2,
    Gif = 3,
    WebP = 4,
    Avif = 5,
    Heic = 6,
    Bmp = 7,
    Tiff = 8,

    /// <summary>Vector. Never rasterised, never served inline — see the security guide.</summary>
    Svg = 9
}
