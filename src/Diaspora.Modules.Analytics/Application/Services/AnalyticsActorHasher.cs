using System.Security.Cryptography;
using System.Text;
using Diaspora.Modules.Analytics.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// Turns a member into an analytics actor key.
/// <para>
/// <b>Keyed, deterministic, and one-way — and each of the three is load-bearing.</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Keyed</b>, because an unkeyed hash of a Guid is reversible by anybody holding the user
///     table: hash every identifier you already have and you have the mapping back. With a
///     deployment secret the analytics store cannot name a reader on its own, which is what makes
///     "there is no per-viewer endpoint" a property of the data rather than of the API surface.
///   </item>
///   <item>
///     <b>Deterministic</b>, because two things need it. A distinct-viewer count has to know that
///     two views were the same person, and an erasure request has to be able to find every row
///     about one member — which it does by computing the key once and deleting by it.
///   </item>
///   <item>
///     <b>One-way</b>, because the alternative is encryption, and an encrypted identifier is a
///     readership record with a key held somewhere on the same infrastructure.
///   </item>
/// </list>
/// <para>
/// <b>This is pseudonymisation, not anonymisation, and the retention policy says so.</b> A key is
/// stable across days, so a row is linkable to other rows about the same person; what it is not is
/// linkable to a person without the secret. Claiming anonymity for a stable pseudonym is the
/// commonest overstatement in analytics privacy documents and this platform does not make it.
/// </para>
/// </summary>
public sealed class AnalyticsActorHasher
{
    private readonly byte[] _key;

    public AnalyticsActorHasher(IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _key = Encoding.UTF8.GetBytes(options.Value.ActorHashKey);
    }

    /// <summary>
    /// The key for a member.
    /// <para>
    /// The identifier is written in its canonical <c>D</c> form before hashing, so two call sites
    /// that formatted a Guid differently cannot produce two keys for one person — which would show
    /// up as a distinct-viewer count that quietly doubled.
    /// </para>
    /// </summary>
    public string Hash(Guid userId)
    {
        var payload = Encoding.UTF8.GetBytes(userId.ToString("D"));

        return Convert.ToBase64String(HMACSHA256.HashData(_key, payload));
    }

    /// <summary>The key for a member, or null when there is no member. Never a shared bucket.</summary>
    public string? HashOrNull(Guid? userId) =>
        userId is null || userId == Guid.Empty ? null : Hash(userId.Value);
}
