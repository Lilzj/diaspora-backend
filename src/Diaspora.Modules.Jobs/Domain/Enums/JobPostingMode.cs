namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// How a posting was written, and so how a member responds to it.
/// <para>
/// <b>Simple</b> is short one-off work, "two cleaners needed in Port Louis, 200 rupees a day,
/// one day" (owner's request, 4 Nov 2026): a few fields, a start date and a length, and an
/// "I'm interested" that opens a conversation with the poster. <b>Standard</b> is the full
/// posting with its application flow, and is what every posting before simple jobs is.
/// </para>
/// </summary>
public enum JobPostingMode
{
    Standard = 0,

    Simple = 1
}
