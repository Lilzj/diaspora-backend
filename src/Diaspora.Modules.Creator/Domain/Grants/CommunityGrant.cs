using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Grants;

/// <summary>
/// A grant programme, with the form somebody applies to it on.
/// <para>
/// <b><see cref="Fields"/> is the load-bearing part and the register says so:</b>
/// "<c>GET /grants/{id}/form</c> returning <c>GrantApplicationField[]</c> is load-bearing. It is
/// what makes adding a grant with eleven questions and a dropdown a backend change rather than an
/// app release." The form is rows on this aggregate; the client renders whatever it is handed and
/// the server validates against the same rows.
/// </para>
/// <para>
/// <b>What is on offer is <c>Money</c> in the platform's shape — minor units, currency and
/// exponent — and this module never moves it.</b> An approved application carries an awarded
/// amount and publishes it; B15 executes. See <c>Application.Financial.GrantFundingBoundary</c> for
/// the documented path and why it is an operator-mediated one.
/// </para>
/// </summary>
public sealed class CommunityGrant : AggregateRoot<CommunityGrantId>, IAuditableEntity
{
    private readonly List<GrantApplicationField> _fields = [];
    private readonly List<GrantFieldOption> _fieldOptions = [];
    private readonly List<GrantDeclaration> _declarations = [];
    private readonly List<GrantDeliverable> _deliverables = [];
    private readonly List<GrantCountry> _countries = [];

    private CommunityGrant(
        CommunityGrantId id,
        string key,
        string title,
        string summary,
        string description,
        string category,
        long fundingMinorUnits,
        string currency) : base(id)
    {
        Key = key;
        Title = title;
        Summary = summary;
        Description = description;
        Category = category;
        FundingMinorUnits = fundingMinorUnits;
        Currency = currency;
    }

    private CommunityGrant() { }

    /// <summary>Stable and unique. What a seeder updates a grant by.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    // ── What is on offer ────────────────────────────────────────────────────────────────────

    public long FundingMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = 2;

    /// <summary>Set together where the programme awards a variable amount. Null otherwise.</summary>
    public long? FundingMinimumMinorUnits { get; private set; }

    public long? FundingMaximumMinorUnits { get; private set; }

    // ── The window ──────────────────────────────────────────────────────────────────────────

    public DateTimeOffset? OpensAt { get; private set; }

    public DateTimeOffset? DeadlineAt { get; private set; }

    public bool IsPublished { get; private set; }

    /// <summary>Whether the form lets an applicant ask for an amount.</summary>
    public bool AllowsAmountRequest { get; private set; }

    /// <summary>Whether the form accepts supporting material by reference.</summary>
    public bool AllowsAttachments { get; private set; }

    /// <summary>How many applications this programme has had. Disclosed where the platform chooses.</summary>
    public int ApplicantCount { get; private set; }

    /// <summary>The lowest creator status that may apply.</summary>
    public CreatorStatus MinimumStatus { get; private set; } = CreatorStatus.NotACreator;

    /// <summary>Whether the applicant must hold creator-programme verification.</summary>
    public bool RequiresVerifiedCreator { get; private set; }

    public IReadOnlyList<GrantApplicationField> Fields => _fields.AsReadOnly();

    /// <summary>
    /// Every allowed value across every select field, keyed back by <c>FieldKey</c>.
    /// <para>
    /// Flat on the grant rather than nested inside the field, for the reason
    /// <c>AcademyLesson.BlockItems</c> gives: an owned collection inside an owned collection is a
    /// shape this platform has never shipped, and a JSON column is one §27 forbids.
    /// </para>
    /// </summary>
    public IReadOnlyList<GrantFieldOption> FieldOptions => _fieldOptions.AsReadOnly();

    public IReadOnlyList<GrantDeclaration> Declarations => _declarations.AsReadOnly();

    public IReadOnlyList<GrantDeliverable> Deliverables => _deliverables.AsReadOnly();

    /// <summary>Countries this grant is open in. Empty means everywhere.</summary>
    public IReadOnlyList<GrantCountry> Countries => _countries.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CommunityGrant Define(
        CommunityGrantId id,
        string key,
        string title,
        string summary,
        string description,
        string category,
        long fundingMinorUnits,
        string currency,
        DateTimeOffset? opensAt = null,
        DateTimeOffset? deadlineAt = null,
        bool allowsAmountRequest = false,
        bool allowsAttachments = true,
        CreatorStatus minimumStatus = CreatorStatus.Contributor,
        bool requiresVerifiedCreator = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new CommunityGrant(
            id,
            key.Trim(),
            title.Trim(),
            summary.Trim(),
            description.Trim(),
            category.Trim(),
            fundingMinorUnits,
            currency.Trim().ToUpperInvariant())
        {
            OpensAt = opensAt,
            DeadlineAt = deadlineAt,
            AllowsAmountRequest = allowsAmountRequest,
            AllowsAttachments = allowsAttachments,
            MinimumStatus = minimumStatus,
            RequiresVerifiedCreator = requiresVerifiedCreator
        };
    }

    public CommunityGrant WithRange(long minimumMinorUnits, long maximumMinorUnits)
    {
        FundingMinimumMinorUnits = minimumMinorUnits;
        FundingMaximumMinorUnits = maximumMinorUnits;
        AllowsAmountRequest = true;
        return this;
    }

    public CommunityGrant WithField(
        string key,
        string label,
        GrantFieldKind kind,
        bool isRequired,
        string? helpText = null,
        int? maxLength = null,
        IEnumerable<(string Value, string Label)>? options = null)
    {
        var field = GrantApplicationField.Create(
            key, label, kind, isRequired, helpText, maxLength, _fields.Count);

        _fields.Add(field);

        if (options is not null)
        {
            var order = 0;

            foreach (var (value, optionLabel) in options)
            {
                _fieldOptions.Add(GrantFieldOption.Create(field.Key, value, optionLabel, order));
                order++;
            }
        }

        return this;
    }

    public CommunityGrant WithDeclaration(string text)
    {
        _declarations.Add(GrantDeclaration.Create(text, _declarations.Count));
        return this;
    }

    public CommunityGrant WithDeliverable(string text)
    {
        _deliverables.Add(GrantDeliverable.Create(text, _deliverables.Count));
        return this;
    }

    public CommunityGrant InCountries(params string[] countryCodes)
    {
        ArgumentNullException.ThrowIfNull(countryCodes);

        foreach (var code in countryCodes.Where(code => !string.IsNullOrWhiteSpace(code)))
        {
            _countries.Add(GrantCountry.Create(code));
        }

        return this;
    }

    public CommunityGrant Publish()
    {
        IsPublished = true;
        return this;
    }

    /// <summary>Rewrites the definition in place, for a re-run of the seeder.</summary>
    public void Redefine(
        string title,
        string summary,
        string description,
        string category,
        long fundingMinorUnits,
        string currency,
        DateTimeOffset? opensAt,
        DateTimeOffset? deadlineAt,
        bool allowsAmountRequest,
        bool allowsAttachments,
        CreatorStatus minimumStatus,
        bool requiresVerifiedCreator,
        long? minimumMinorUnits,
        long? maximumMinorUnits,
        IEnumerable<GrantApplicationField> fields,
        IEnumerable<GrantFieldOption> fieldOptions,
        IEnumerable<GrantDeclaration> declarations,
        IEnumerable<GrantDeliverable> deliverables,
        IEnumerable<GrantCountry> countries)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(fieldOptions);
        ArgumentNullException.ThrowIfNull(declarations);
        ArgumentNullException.ThrowIfNull(deliverables);
        ArgumentNullException.ThrowIfNull(countries);

        Title = title.Trim();
        Summary = summary.Trim();
        Description = description.Trim();
        Category = category.Trim();
        FundingMinorUnits = fundingMinorUnits;
        Currency = currency.Trim().ToUpperInvariant();
        OpensAt = opensAt;
        DeadlineAt = deadlineAt;
        AllowsAmountRequest = allowsAmountRequest;
        AllowsAttachments = allowsAttachments;
        MinimumStatus = minimumStatus;
        RequiresVerifiedCreator = requiresVerifiedCreator;
        FundingMinimumMinorUnits = minimumMinorUnits;
        FundingMaximumMinorUnits = maximumMinorUnits;
        IsPublished = true;

        _fields.Clear();
        _fields.AddRange(fields);

        _fieldOptions.Clear();
        _fieldOptions.AddRange(fieldOptions);

        _declarations.Clear();
        _declarations.AddRange(declarations);

        _deliverables.Clear();
        _deliverables.AddRange(deliverables);

        _countries.Clear();
        _countries.AddRange(countries);
    }

    public void RecordApplication() => ApplicantCount++;

    /// <summary>
    /// Where this grant is in its own life.
    /// <para>
    /// <see cref="CommunityGrantState.ClosingSoon"/> is decided here rather than on a device,
    /// because "soon" is the programme's word and a client computing it from a deadline would be
    /// choosing what soon means.
    /// </para>
    /// </summary>
    public CommunityGrantState StateAt(DateTimeOffset now)
    {
        if (!IsPublished)
        {
            return CommunityGrantState.Upcoming;
        }

        if (OpensAt is { } opens && now < opens)
        {
            return CommunityGrantState.Upcoming;
        }

        if (DeadlineAt is not { } deadline)
        {
            return CommunityGrantState.Open;
        }

        if (now > deadline)
        {
            return CommunityGrantState.Closed;
        }

        return now >= deadline.AddDays(-CreatorLimits.GrantClosingSoonDays)
            ? CommunityGrantState.ClosingSoon
            : CommunityGrantState.Open;
    }

    /// <summary>Whether an application may be submitted right now, ignoring who is applying.</summary>
    public bool IsAcceptingApplications(DateTimeOffset now) =>
        StateAt(now) is CommunityGrantState.Open or CommunityGrantState.ClosingSoon;

    public GrantApplicationField? FindField(string key) =>
        _fields.Find(field => string.Equals(field.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>The allowed values on one field, in order.</summary>
    public IReadOnlyList<GrantFieldOption> OptionsFor(string fieldKey) =>
        _fieldOptions
            .Where(option => string.Equals(option.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(option => option.SortOrder)
            .ToList();

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}

/// <summary>
/// One question on a grant's application form.
/// <para>
/// Served to the client and enforced on the server against the same row. §10 of the phase brief:
/// "The backend must validate required fields, field types, allowed values …" — all three are
/// columns here rather than rules in a handler.
/// </para>
/// </summary>
public sealed class GrantApplicationField : ValueObject
{
    private GrantApplicationField(
        string key,
        string label,
        GrantFieldKind kind,
        bool isRequired,
        string? helpText,
        int? maxLength,
        int sortOrder)
    {
        Key = key;
        Label = label;
        Kind = kind;
        IsRequired = isRequired;
        HelpText = helpText;
        MaxLength = maxLength;
        SortOrder = sortOrder;
    }

    private GrantApplicationField() { }

    public string Key { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string? HelpText { get; private set; }

    public GrantFieldKind Kind { get; private set; }

    public bool IsRequired { get; private set; }

    public int? MaxLength { get; private set; }

    public int SortOrder { get; private set; }

    public static GrantApplicationField Create(
        string key,
        string label,
        GrantFieldKind kind,
        bool isRequired,
        string? helpText,
        int? maxLength,
        int sortOrder) =>
        new(key.Trim(), label.Trim(), kind, isRequired, helpText?.Trim(), maxLength, sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
    }
}

/// <summary>
/// One allowed value on a select field.
/// <para>
/// Owned by the grant rather than by the field, and keyed back to it by <see cref="FieldKey"/>.
/// See <c>CommunityGrant.FieldOptions</c> for why.
/// </para>
/// </summary>
public sealed class GrantFieldOption : ValueObject
{
    private GrantFieldOption(string fieldKey, string value, string label, int sortOrder)
    {
        FieldKey = fieldKey;
        Value = value;
        Label = label;
        SortOrder = sortOrder;
    }

    private GrantFieldOption() { }

    public string FieldKey { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static GrantFieldOption Create(
        string fieldKey, string value, string label, int sortOrder) =>
        new(fieldKey.Trim(), value.Trim(), label.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FieldKey;
        yield return Value;
    }
}

/// <summary>Something an applicant has to agree to.</summary>
public sealed class GrantDeclaration : ValueObject
{
    private GrantDeclaration(string key, string text, int sortOrder)
    {
        Key = key;
        Text = text;
        SortOrder = sortOrder;
    }

    private GrantDeclaration() { }

    /// <summary>What the applicant's acceptance names. Derived from the position, so it is stable.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static GrantDeclaration Create(string text, int sortOrder) =>
        new($"declaration-{sortOrder + 1}", text.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
    }
}

/// <summary>Something a successful applicant has to deliver.</summary>
public sealed class GrantDeliverable : ValueObject
{
    private GrantDeliverable(string text, int sortOrder)
    {
        Text = text;
        SortOrder = sortOrder;
    }

    private GrantDeliverable() { }

    public string Text { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public static GrantDeliverable Create(string text, int sortOrder) => new(text.Trim(), sortOrder);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SortOrder;
        yield return Text;
    }
}

/// <summary>A country a grant is open in.</summary>
public sealed class GrantCountry : ValueObject
{
    private GrantCountry(string countryCode) => CountryCode = countryCode;

    private GrantCountry() { }

    public string CountryCode { get; private set; } = string.Empty;

    public static GrantCountry Create(string countryCode) =>
        new(countryCode.Trim().ToUpperInvariant());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CountryCode;
    }
}
