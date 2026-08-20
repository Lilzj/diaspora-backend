using Diaspora.Modules.Notifications.Domain.Digests;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Extensibility.Digests;

/// <summary>
/// Assembles one member's collected messages for one window.
/// <para>
/// <b>Channel-independent, which the brief requires and which is also the only shape that works.</b>
/// A digest is a list of things that happened. Whether it becomes an email, one push or a card at
/// the top of the inbox is decided afterwards by the same preference logic every other message goes
/// through. Generating per channel would mean generating three times and having the three disagree
/// about what "yesterday" meant.
/// </para>
/// <para>
/// A port so that a deployment can group, rank or summarise differently — "the three most important
/// things" instead of "everything, newest first" is a different generator and no other change. It is
/// also where an AI summariser would land, which is why the brief lists AI summaries as out of scope
/// rather than impossible.
/// </para>
/// </summary>
public interface IDigestGenerator
{
    Task<Result<NotificationDigest>> GenerateAsync(
        DigestRequest request, CancellationToken cancellationToken = default);
}
