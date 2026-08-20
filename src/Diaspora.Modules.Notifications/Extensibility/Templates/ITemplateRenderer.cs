using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Extensibility.Templates;

/// <summary>
/// Turns a template and a set of values into the words a member reads.
/// <para>
/// <b>A port rather than a static helper, because rendering is the part of this module most likely
/// to be replaced.</b> A deployment that wants ICU message format, or a house Markdown dialect, or
/// a renderer that consults a glossary, swaps this. Everything above it — the dispatcher, the
/// digest builder, the preview endpoint — is written against the interface.
/// </para>
/// <para>
/// <b>It returns a <see cref="Result{T}"/> rather than throwing.</b> A missing required variable is
/// an ordinary failure with a member on the other end of it, and the dispatcher's response is to
/// record the notification as failed rather than to lose the whole batch.
/// </para>
/// </summary>
public interface ITemplateRenderer
{
    Result<RenderedTemplate> Render(TemplateRenderRequest request);
}
