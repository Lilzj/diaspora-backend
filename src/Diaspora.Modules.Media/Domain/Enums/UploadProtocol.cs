namespace Diaspora.Modules.Media.Domain.Enums;

/// <summary>
/// How the bytes are going to arrive.
/// <para>
/// Chosen by the server at session creation from the declared size and the storage provider's
/// capabilities, not requested by the client. A client that had to pick would have to know
/// whether the deployment's provider supports presigned PUT — which is exactly the
/// provider-specific knowledge the storage abstraction exists to keep out of callers.
/// </para>
/// </summary>
public enum UploadProtocol
{
    /// <summary>One request, whole file. Small images — the common case in this app.</summary>
    Direct = 0,

    /// <summary>Sequential parts through the API, resumable. The default above the direct ceiling.</summary>
    Chunked = 1,

    /// <summary>Parts uploaded straight to the provider, assembled by it. Large video.</summary>
    Multipart = 2,

    /// <summary>The client PUTs to a provider-signed URL and tells us when it is done.</summary>
    PresignedDirect = 3
}
