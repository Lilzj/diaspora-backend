using Diaspora.Application.Identity.Models;
using Diaspora.Domain.Identity.Verification;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Delivers a one-time code to the member. B2 shipped a logging implementation; an SMS
/// gateway and an email gateway drop in behind the same port without touching any use case.
/// </summary>
public interface IOtpDeliveryService
{
    Task<bool> SendAsync(OtpDeliveryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which wire a code for <paramref name="purpose"/> would travel on, given what this
    /// deployment has configured.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A use case asks so it can tell the member where to look. It is deliberately a function of
    /// <em>configuration and purpose only</em> — never of the account — because the one caller
    /// that needs it is the unauthenticated recovery endpoint, and an answer that varied per
    /// account would turn its acknowledgement into an account-enumeration oracle.
    /// </para>
    /// <para>
    /// <see cref="OtpDeliveryChannel.None"/> is an honest answer, and the reason this returns a
    /// channel rather than a bool: a deployment with no transport at all should say so rather
    /// than promise a message.
    /// </para>
    /// </remarks>
    OtpDeliveryChannel ChannelFor(OtpPurpose purpose);
}
