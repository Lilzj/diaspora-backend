using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Domain.ValueObjects;

/// <summary>The failures <see cref="MediaContentType"/> can return.</summary>
internal static class MediaContentTypeErrors
{
    public static readonly Error Missing = Error.Validation(
        MediaErrorCodes.ContentTypeNotAllowed, "A content type is required.");

    public static readonly Error Malformed = Error.Validation(
        MediaErrorCodes.ContentTypeNotAllowed, "That is not a valid media type.");
}
