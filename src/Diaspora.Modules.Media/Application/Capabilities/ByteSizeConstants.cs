namespace Diaspora.Modules.Media.Application.Capabilities;

/// <summary>
/// Unit conversions for turning configured megabytes into the bytes a client is told about.
/// <para>
/// Named rather than inlined because <c>1024 * 1024</c> appearing in four places is four
/// chances for one of them to be <c>1000 * 1000</c>, and the resulting off-by-4.8% limit
/// mismatch between what the client enforces and what the server enforces is exactly the kind
/// of bug that only shows up on files near the boundary.
/// </para>
/// </summary>
internal static class ByteSizeConstants
{
    public const long BytesPerMegabyte = 1024 * 1024;
}
