using Diaspora.Platform.Services.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Diaspora.Api.Identity.Context;

/// <summary>
/// Backs the platform kernel's <c>IRequestContext</c> with the live HTTP request.
/// <para>
/// Replaces B1's placeholder, which reported nulls for everything. Identity depends on
/// these values for real work: the IP address and user agent appear in security events and
/// in the member's session list, and the language drives localised validation messages.
/// </para>
/// </summary>
public sealed class HttpContextRequestContext : IRequestContext
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string DeviceIdHeader = "X-Device-Id";
    public const string PlatformHeader = "X-Platform";
    public const string AppVersionHeader = "X-App-Version";
    public const string TimezoneHeader = "X-Timezone";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    public string CorrelationId =>
        Header(CorrelationIdHeader) ?? Context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    public string RequestId => Context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");

    public string? Culture => Language;

    public string? Timezone => Header(TimezoneHeader);

    public string? DeviceId => Header(DeviceIdHeader);

    public string? Platform => Header(PlatformHeader);

    public string? AppVersion => Header(AppVersionHeader);

    /// <summary>
    /// The first language from Accept-Language, stripped of its quality weight. Good
    /// enough to pick a translation; full content negotiation would be over-engineering
    /// for validation messages.
    /// </summary>
    public string? Language
    {
        get
        {
            var header = Context?.Request.Headers.AcceptLanguage.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(header)) return null;

            var first = header.Split(',', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return first?.Split(';')[0].Trim();
        }
    }

    /// <summary>
    /// The peer address. Behind a load balancer this is the proxy unless forwarded-header
    /// processing is configured upstream — the platform host owns that decision, so this
    /// deliberately reports what the socket says rather than trusting a client header.
    /// </summary>
    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.FirstOrDefault();

    private string? Header(string name)
    {
        var value = Context?.Request.Headers[name].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
