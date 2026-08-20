using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Extensibility.Views;

/// <summary>
/// Decides whether a view is a new one.
/// <para>
/// A seam rather than a method because the shipped implementation is in-process and therefore
/// wrong the moment there are two web instances: two members' views land on two nodes and neither
/// knows about the other's. A distributed tracker backed by a cache implements this and replaces
/// it, and nothing above the port changes.
/// </para>
/// <para>
/// <b>It is also the one seam whose whole value is outliving a request.</b> A per-request tracker
/// would de-duplicate nothing at all.
/// </para>
/// </summary>
public interface IHousingViewTracker
{
    /// <summary>
    /// Records a view and says whether it was unique inside the configured window.
    /// <para>
    /// A signed-out visitor has no identity to de-duplicate on, so every anonymous view counts as
    /// unique. That overstates unique views on public traffic and is the honest limit of what can
    /// be known without tracking people who have not signed in — which this platform does not do.
    /// </para>
    /// </summary>
    bool RecordView(HousingListingId listingId, UserId? viewerId, DateTimeOffset now);
}
