namespace Diaspora.Modules.Media;

/// <summary>
/// The feature gates the Media module owns.
/// <para>
/// Gates tell a client what to render; the permissions decide what the server will do. The
/// pairing is what lets the app grey out the "Add video" button with a reason instead of
/// offering it and failing at the end of a 200 MB upload.
/// </para>
/// </summary>
public static class MediaFeatures
{
    public const string Upload = "media.upload";
    public const string PhotoUpload = "media.upload.photo";
    public const string DocumentUpload = "media.upload.document";
    public const string VideoUpload = "media.upload.video";
    public const string AudioUpload = "media.upload.audio";
    public const string ResumableUpload = "media.upload.resumable";
    public const string Galleries = "media.galleries";
    public const string Transformations = "media.transformations";
    public const string SignedUrls = "media.signed-urls";
}
