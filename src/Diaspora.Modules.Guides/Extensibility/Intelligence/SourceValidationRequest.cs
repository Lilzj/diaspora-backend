using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// A claim about where an official statement came from, offered for checking.
/// <para>
/// Flat primitives, no aggregate, no identifier of a person — a validator may be a class in another
/// assembly, an HTTP call to a deployment's own allow-list service, or a rule in a configuration
/// file, and the shape has to survive all three.
/// </para>
/// <para>
/// <b>The host is supplied already parsed.</b> Every validator asking "is this a government domain"
/// would otherwise parse the URL itself, and two of them would eventually disagree about what
/// <c>HTTPS://GOV.MU:443/x</c> is.
/// </para>
/// </summary>
public sealed record SourceValidationRequest
{
    /// <summary>Upper-case ISO 3166-1 alpha-2 — the country the notice is about.</summary>
    public required string CountryCode { get; init; }

    /// <summary>What kind of body is claimed to have issued this.</summary>
    public required AuthorityType AuthorityType { get; init; }

    /// <summary>The authority's name, as recorded.</summary>
    public required string AuthorityName { get; init; }

    /// <summary>Whether Trust has confirmed the authority is what it claims.</summary>
    public required bool AuthorityIsVerified { get; init; }

    /// <summary>The official source URL, already normalized. Null when none was supplied.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>The source URL's lower-cased host, already parsed. Null when there is no URL.</summary>
    public string? SourceHost { get; init; }

    /// <summary>The authority's own reference number, as recorded.</summary>
    public string? ReferenceNumber { get; init; }
}
