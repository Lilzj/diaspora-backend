using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Media.Domain.Identifiers;

/// <summary>
/// Identifies an in-flight upload.
/// <para>
/// Distinct from the <see cref="Diaspora.SharedKernel.Identity.MediaId"/> the session
/// eventually produces, and deliberately so: a client that resumes an upload after a
/// reinstall holds a session id, and handing it the asset id before the bytes are all there
/// would be handing it an identifier for something that does not exist.
/// </para>
/// </summary>
public sealed record UploadSessionId(Guid Value) : StronglyTypedId(Value)
{
    public static UploadSessionId New() => new(Guid.NewGuid());

    public static UploadSessionId From(Guid value) => new(value);
}
