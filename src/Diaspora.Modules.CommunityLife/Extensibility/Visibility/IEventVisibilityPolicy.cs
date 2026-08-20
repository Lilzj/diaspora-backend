namespace Diaspora.Modules.CommunityLife.Extensibility.Visibility;

/// <summary>
/// Who may see an event.
/// <para>
/// A seam because visibility rules are the ones most likely to need a deployment-specific answer: a
/// platform operating somewhere with a hostile government may need religious and political events
/// invisible to anybody outside the organization, and that is a decision an operator makes rather
/// than one this module should assume either way.
/// </para>
/// <para>
/// <b>Every registered policy must agree, and a deny is final.</b> Composing visibility any other
/// way would let a permissive policy added later quietly widen an audience an earlier one narrowed
/// deliberately.
/// </para>
/// </summary>
public interface IEventVisibilityPolicy
{
    string Name { get; }

    /// <summary>
    /// Whether the reader may see the event. Return null to abstain, which leaves the decision to
    /// the module's own rules.
    /// </summary>
    bool? CanView(EventVisibilityContext context);
}
