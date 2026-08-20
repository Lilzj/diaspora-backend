namespace Diaspora.Modules.CommunityLife.Extensibility.Views;

/// <summary>
/// How a repeat view is told from a first one.
/// <para>
/// A seam because the right implementation is a deployment decision: the shipped one is an in-memory
/// window, which is honest for a single host and wrong for three behind a load balancer, where a
/// Redis-backed tracker belongs. Nothing above this interface changes when one arrives.
/// </para>
/// <para>
/// <b>The one seam whose whole value is outliving a request.</b> A per-request view tracker would
/// de-duplicate nothing, which is why this is the module's only singleton registration — and it can
/// safely be one because it holds a concurrent dictionary and no scoped dependency.
/// </para>
/// </summary>
public interface ICommunityLifeViewTracker
{
    /// <summary>
    /// Records a view and says whether it was the first from this viewer in the window.
    /// <para>
    /// <paramref name="viewerKey"/> is a user identifier when there is one and a hashed client
    /// fingerprint when there is not. Never a raw address: a view record is not a reason to keep
    /// one.
    /// </para>
    /// </summary>
    bool RecordView(Guid subjectId, string viewerKey);
}
