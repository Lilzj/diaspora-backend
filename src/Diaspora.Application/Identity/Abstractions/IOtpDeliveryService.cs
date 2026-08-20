using Diaspora.Application.Identity.Models;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// Delivers a one-time code to the member. B2 ships a logging implementation; an SMS
/// gateway drops in behind the same port without touching any use case.
/// </summary>
public interface IOtpDeliveryService
{
    Task<bool> SendAsync(OtpDeliveryRequest request, CancellationToken cancellationToken = default);
}
