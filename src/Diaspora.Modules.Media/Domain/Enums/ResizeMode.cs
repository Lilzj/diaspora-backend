namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>How a resize should treat an aspect-ratio mismatch.</summary>
public enum ResizeMode
{
    /// <summary>Fit inside the box, preserving aspect ratio. Never crops, never pads.</summary>
    Contain = 0,

    /// <summary>Fill the box, preserving aspect ratio, cropping the overflow. Square avatars.</summary>
    Cover = 1,

    /// <summary>Stretch to the exact box. Distorts; offered because some callers want it.</summary>
    Stretch = 2,

    /// <summary>Fit inside the box and pad the remainder. Used for fixed-frame posters.</summary>
    Pad = 3
}
