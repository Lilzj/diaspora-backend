using System.ComponentModel.DataAnnotations;

namespace Diaspora.Api.Platform.Outbox;

/// <summary>
/// The body of a skip.
/// <para>
/// The reason is required, and it is required because this is the one operation in the platform
/// that stops a committed business event from reaching its consumers. It goes into the
/// dead-letter row and into the audit log, and "no reason given" is not an acceptable entry
/// against a decision somebody will have to explain.
/// </para>
/// </summary>
public sealed record OutboxSkipRequest
{
    [Required]
    [StringLength(512, MinimumLength = 3)]
    public required string Reason { get; init; }
}
