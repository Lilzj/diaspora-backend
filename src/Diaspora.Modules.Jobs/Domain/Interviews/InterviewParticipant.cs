using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Interviews;

/// <summary>
/// One interviewer on the employer's side.
/// <para>
/// <b>The name is free text and the user identifier is optional</b>, because an interview panel
/// routinely includes people who are not platform members — a ward manager, an external
/// assessor, a client of the agency. Requiring an account for each would mean employers simply
/// stopped listing them, and the candidate would lose the one piece of information that lets
/// them prepare.
/// </para>
/// </summary>
public sealed class InterviewParticipant
{
    private InterviewParticipant(
        Guid participantId, UserId? userId, string name, string? role, int order)
    {
        ParticipantId = participantId;
        UserId = userId;
        Name = name;
        Role = role;
        Order = order;
    }

    private InterviewParticipant() { }

    public Guid ParticipantId { get; private set; }

    /// <summary>The platform member, when the interviewer is one. Often null.</summary>
    public UserId? UserId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    /// <summary>What they do — "Ward Manager", "Head of Engineering". Helps a candidate prepare.</summary>
    public string? Role { get; private set; }

    public int Order { get; private set; }

    internal static InterviewParticipant? Create(
        Guid participantId, UserId? userId, string? name, string? role, int order)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var display = name.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            display = display[..JobLimits.MaxDisplayName].TrimEnd();
        }

        var title = string.IsNullOrWhiteSpace(role) ? null : role.Trim();

        if (title is { Length: > JobLimits.MaxDisplayName })
        {
            title = title[..JobLimits.MaxDisplayName].TrimEnd();
        }

        return new InterviewParticipant(
            participantId == Guid.Empty ? Guid.NewGuid() : participantId,
            userId, display, title, order);
    }
}
