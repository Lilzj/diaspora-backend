namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>A record that content was passed along.</summary>
public sealed record CreateShareRequest
{
    /// <summary><c>internal</c>, <c>external</c>, <c>quote</c> or <c>reshare</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Self-declared, for an external share. Never verified by the server.</summary>
    public string? Destination { get; init; }

    /// <summary>The member or conversation, for an internal share.</summary>
    public Guid? RecipientId { get; init; }

    /// <summary>Commentary, for a quote share. Becomes the body of the new post.</summary>
    public string? Message { get; init; }
}
