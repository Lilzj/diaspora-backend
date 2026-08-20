using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Resumes;

/// <summary>
/// One version of a candidate's résumé.
/// <para>
/// <b>Its own aggregate rather than a collection on the profile, and the reason is the
/// application.</b> An application references the exact résumé it was sent with, and that
/// reference has to stay valid and stay <em>unchanged</em> for years — an employer looking at a
/// six-month-old application must see the document they were actually sent, not whatever the
/// candidate has uploaded since. A collection on the profile would make that a snapshot problem;
/// a separate, immutable-in-content aggregate makes it a foreign key.
/// </para>
/// <para>
/// <b>The bytes are Media's, always.</b> This holds a <see cref="MediaId"/>, a label and some
/// metadata Media reported. B9 never sees a stream, never stores a URL, and does no parsing —
/// résumé parsing is explicitly out of scope for this phase, and the <see cref="Resume"/>
/// aggregate is deliberately shaped so that adding it later is a new module reading this
/// reference rather than a change here.
/// </para>
/// <para>
/// <b>Exactly one résumé per candidate is the default.</b> The invariant is enforced by the
/// repository and the command rather than here, because it spans instances — an aggregate
/// cannot see its siblings.
/// </para>
/// </summary>
public sealed class Resume
    : AggregateRoot<ResumeId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private Resume(
        ResumeId id,
        CandidateProfileId candidateId,
        UserId userId,
        Guid mediaId,
        string label,
        string? fileName,
        long? sizeInBytes,
        string? contentType,
        DateTimeOffset createdAt)
        : base(id)
    {
        CandidateId = candidateId;
        UserId = userId;
        MediaId = mediaId;
        Label = label;
        FileName = fileName;
        SizeInBytes = sizeInBytes;
        ContentType = contentType;
        CreatedAt = createdAt;
    }

    private Resume() { }

    public CandidateProfileId CandidateId { get; private set; } = default!;

    /// <summary>The member. Denormalised so an ownership check needs no join.</summary>
    public UserId UserId { get; private set; } = default!;

    /// <summary>The platform's ownership seam.</summary>
    public Guid OwnerId => UserId.Value;

    /// <summary>B5's reference to the document. Never a URL, never bytes.</summary>
    public Guid MediaId { get; private set; }

    /// <summary>What the candidate calls this version. "Nursing — UK", "General".</summary>
    public string Label { get; private set; } = string.Empty;

    /// <summary>The original file name, as Media reported it. Shown to the employer.</summary>
    public string? FileName { get; private set; }

    /// <summary>Size in bytes, as Media reported it. Rendered beside the download.</summary>
    public long? SizeInBytes { get; private set; }

    /// <summary>MIME type, as Media reported it. B9 validates nothing about the file itself.</summary>
    public string? ContentType { get; private set; }

    /// <summary>
    /// Whether this is the version attached when the candidate does not choose one. Exactly one
    /// résumé per candidate carries it; the command enforces that across instances.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>How many applications were sent with this version. A candidate-facing stat.</summary>
    public int UseCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Creates a résumé version pointing at an uploaded document.</summary>
    public static Result<Resume> Create(
        ResumeId id,
        CandidateProfileId candidateId,
        UserId userId,
        Guid mediaId,
        string? label,
        DateTimeOffset now,
        string? fileName = null,
        long? sizeInBytes = null,
        string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(candidateId);
        ArgumentNullException.ThrowIfNull(userId);

        if (mediaId == Guid.Empty)
        {
            return Result.Failure<Resume>(JobErrors.AttachmentInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        if (trimmed is null || trimmed.Length > JobLimits.MaxResumeLabel)
        {
            return Result.Failure<Resume>(JobErrors.ResumeLabelInvalid);
        }

        return Result.Success(new Resume(
            id, candidateId, userId, mediaId, trimmed,
            Clip(fileName, JobLimits.MaxDisplayName),
            sizeInBytes is > 0 ? sizeInBytes : null,
            Clip(contentType, 200),
            now));
    }

    /// <summary>
    /// Renames the version. The only thing about a résumé that can change — the document itself
    /// is immutable, because an application already sent with it must keep pointing at what the
    /// employer actually received. A new document is a new version.
    /// </summary>
    public Result Rename(string? label)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ResumeNotFound);
        }

        var trimmed = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        if (trimmed is null || trimmed.Length > JobLimits.MaxResumeLabel)
        {
            return Result.Failure(JobErrors.ResumeLabelInvalid);
        }

        Label = trimmed;

        return Result.Success();
    }

    /// <summary>Makes this the default version. The command clears the flag on the others.</summary>
    public void MakeDefault() => IsDefault = true;

    /// <summary>Clears the default flag.</summary>
    public void ClearDefault() => IsDefault = false;

    /// <summary>Records that an application was sent with this version.</summary>
    public void RecordUse() => UseCount++;

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
        IsDefault = false;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
