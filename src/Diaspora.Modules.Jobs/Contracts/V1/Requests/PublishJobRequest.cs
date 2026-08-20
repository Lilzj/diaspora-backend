namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Publishes a posting.
/// <para>
/// <see cref="OpenImmediately"/> defaults to true, which is the ordinary case. Publishing without
/// opening is the employer who wants the role visible before the application window starts — a
/// real thing employers do, and one most job products cannot express.
/// </para>
/// </summary>
public sealed record PublishJobRequest
{
    public bool OpenImmediately { get; init; } = true;

    /// <summary>When applications stop. Must be in the future. Null means open until closed.</summary>
    public DateTimeOffset? ClosingAt { get; init; }
}
