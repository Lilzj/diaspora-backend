namespace Diaspora.Contracts.Trust.V1.Submission;

/// <summary>
/// What is being verified.
/// <para>
/// <b>The reason this type exists is that Trust reviews people, but modules own things.</b> A
/// member verifying their own identity and a member verifying the business they run are the same
/// workflow with the same evidence and the same reviewer — but the second one is about a row in
/// another module's table, and Trust had no way to say so. Without a subject, two businesses owned
/// by one member are indistinguishable, and the second submission looks like a duplicate of the
/// first.
/// </para>
/// <para>
/// <see cref="SubjectType"/> is a string rather than an enum deliberately, and it is the same
/// decision Trust's published events made about <c>VerificationType</c>: a Marketplace or Housing
/// module must be able to name its own aggregate without an enum in this assembly gaining a member
/// — which would mean every future vertical requires a change to a frozen contract before it can
/// verify anything.
/// </para>
/// </summary>
public sealed record TrustVerificationSubject
{
    /// <summary>
    /// The kind of thing under review. Use a <see cref="TrustVerificationSubjectTypes"/> constant
    /// where one fits; a module verifying an aggregate the platform has not seen before supplies
    /// its own name.
    /// </summary>
    public required string SubjectType { get; init; }

    /// <summary>
    /// The aggregate's identifier in the owning module.
    /// <para>
    /// Null when the subject is the submitting member themselves — a member has no identifier
    /// separate from <c>SubmittedBy</c>, and inventing one would give the same person two names.
    /// </para>
    /// </summary>
    public Guid? SubjectId { get; init; }

    /// <summary>The member as the subject. What every pre-B3.0.2 submission was, implicitly.</summary>
    public static TrustVerificationSubject Member { get; } =
        new() { SubjectType = TrustVerificationSubjectTypes.Member };

    /// <summary>A named aggregate belonging to another module.</summary>
    public static TrustVerificationSubject For(string subjectType, Guid subjectId) =>
        new() { SubjectType = subjectType, SubjectId = subjectId };
}

/// <summary>
/// The subject types the platform ships knowing about.
/// <para>
/// <b>Not a closed set.</b> These are the ones that exist today; a module may submit any name it
/// owns. They are constants rather than an enum precisely so that adding one is not a change to a
/// frozen contract — the list is a convenience for the modules that are already here, not a
/// gate on the ones that are not.
/// </para>
/// </summary>
public static class TrustVerificationSubjectTypes
{
    /// <summary>The submitting member. The default, and what every B3 submission meant.</summary>
    public const string Member = "Member";

    /// <summary>A service provider listing. B7.</summary>
    public const string Provider = "Provider";

    /// <summary>A community organisation.</summary>
    public const string Organization = "Organization";

    /// <summary>A registered business.</summary>
    public const string Business = "Business";

    /// <summary>An embassy or consulate.</summary>
    public const string Embassy = "Embassy";

    /// <summary>A professional practising credential.</summary>
    public const string Professional = "Professional";
}
