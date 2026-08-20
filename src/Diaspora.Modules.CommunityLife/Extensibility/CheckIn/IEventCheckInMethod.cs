using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Extensibility.CheckIn;

/// <summary>
/// How somebody at a door is identified.
/// <para>
/// <b>The brief asks for provider-independent check-in abstractions that are future-ready for QR,
/// and says explicitly not to implement QR infrastructure. This interface is exactly that line.</b>
/// <see cref="CheckInMethod.QrCode"/> exists in the enum so an attendance record written by a
/// scanner in a year is distinguishable from one a volunteer typed; no implementation of this
/// interface produces it, and none ships.
/// </para>
/// <para>
/// What does ship is manual, organizer and self-confirmed — three classes, each resolving a request
/// to a response and writing nothing. A QR method is a fourth class in whichever assembly owns the
/// scanner, and the aggregate, the handler, the endpoint and the contract are all unchanged when it
/// arrives. That is the whole claim, and it is testable: an architecture test asserts the seam is an
/// interface, and a unit test asserts no registered method returns the QR value.
/// </para>
/// </summary>
public interface IEventCheckInMethod
{
    /// <summary>The method this class implements. Unique across the registered set.</summary>
    CheckInMethod Method { get; }

    /// <summary>
    /// Whether this method can answer the request at all. A cheap synchronous check — a token
    /// method should not be asked about a request with no token.
    /// </summary>
    bool CanHandle(CheckInRequest request);

    /// <summary>
    /// Resolves the arrival to a response, or explains why it could not.
    /// <para>
    /// Never writes. The module applies the outcome, which is what keeps the attendance counters
    /// settled from one place however many methods exist.
    /// </para>
    /// </summary>
    Task<CheckInOutcome> ResolveAsync(
        CheckInRequest request, CancellationToken cancellationToken = default);
}
