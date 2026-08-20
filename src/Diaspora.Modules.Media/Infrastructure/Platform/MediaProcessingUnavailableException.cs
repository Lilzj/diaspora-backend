namespace Diaspora.Modules.Media.Infrastructure.Platform;

/// <summary>
/// Thrown when the platform's <see cref="Diaspora.Platform.Services.Abstractions.IImageProcessingService"/>
/// is asked for something no registered processor can do.
/// <para>
/// A named exception rather than <see cref="NotSupportedException"/> so the message can say
/// which capability is missing and how to add it. The difference matters at three in the
/// morning: "no image processor is registered in this deployment" points at a deployment
/// configuration, while a bare not-supported points at nothing.
/// </para>
/// <para>
/// This exists only on the platform-interface adapter. Media's own paths never throw for a
/// missing capability — they record the step as <c>Unsupported</c> and carry on — because a
/// missing codec must not be a failed upload.
/// </para>
/// </summary>
public sealed class MediaProcessingUnavailableException : InvalidOperationException
{
    public MediaProcessingUnavailableException()
        : base("The requested media processing capability is not available.")
    {
    }

    public MediaProcessingUnavailableException(string message) : base(message)
    {
    }

    public MediaProcessingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
