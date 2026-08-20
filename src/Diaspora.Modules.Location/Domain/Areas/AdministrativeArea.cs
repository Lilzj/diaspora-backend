using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Events;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// One node of the geographic hierarchy below country level: a state, region, district,
/// city, neighbourhood or postal area.
/// <para>
/// <b>Why one aggregate and not six.</b> The brief names State, Region, District, City and
/// Neighborhood as separate domain concepts, and they are — but they are not separate
/// <em>aggregates</em>. Six tables holding the same eight columns would turn "everything
/// inside the United Kingdom" into a five-way union, make adding the already-anticipated
/// Street level a schema migration rather than a row, and force every consumer to know which
/// of six types it holds. One node type carrying its <see cref="AdministrativeLevel"/> gives
/// arbitrary depth for free, which is exactly what real administrative geography needs:
/// England has counties where France has departments and Mauritius has districts with no
/// intervening state at all.
/// </para>
/// <para>
/// The named concepts survive as the typed views in this folder — <see cref="City"/>,
/// <see cref="Neighborhood"/>, <see cref="Region"/> and the rest — which give level-specific
/// behaviour and compile-time safety over the same aggregate. Recorded as an intentional
/// design deviation in the geographic domain model document.
/// </para>
/// <para>
/// <b>The invariant.</b> A node's parent must sit at a strictly lower level. That single rule
/// is what keeps the tree acyclic, keeps <see cref="Path"/> honest, and makes it impossible
/// to hang London off Peckham.
/// </para>
/// </summary>
public sealed class AdministrativeArea : AggregateRoot<AreaId>, IAuditableEntity, ISoftDeletable
{
    public AdministrativeLevel Level { get; private set; }

    public PlaceName Name { get; private set; } = default!;

    /// <summary>Unique within the parent, not globally — there is a Springfield in twelve states.</summary>
    public LocationSlug Slug { get; private set; } = default!;

    /// <summary>The country this node sits in. Denormalised so country filters need no join.</summary>
    public CountryId CountryId { get; private set; } = default!;

    /// <summary>The ISO code of that country, for filtering without a join.</summary>
    public CountryCode CountryCode { get; private set; } = default!;

    /// <summary>Null for a node hanging directly off its country.</summary>
    public AreaId? ParentId { get; private set; }

    /// <summary>
    /// The materialised ancestor path, in its stored form.
    /// <para>
    /// A plain string rather than a converted <see cref="HierarchyPath"/>, and that is a
    /// query-translation decision rather than a modelling one. Every hierarchy question in the
    /// module is a prefix match — <c>WHERE path LIKE '/GB/london/%'</c> — and EF cannot
    /// translate a <c>StartsWith</c> against a member of a value-converted type. Storing the
    /// string and projecting the value object over it keeps the domain expressive and the
    /// query index-eligible; the alternative is a recursive CTE on every discovery query.
    /// </para>
    /// </summary>
    public string PathValue { get; private set; } = default!;

    /// <summary>
    /// The ancestor path as a value object. Not mapped — projected from
    /// <see cref="PathValue"/>, and cached so a breadcrumb render does not re-parse it per row.
    /// </summary>
    public HierarchyPath Path
    {
        get
        {
            if (_path is null || !string.Equals(_path.Value, PathValue, StringComparison.Ordinal))
            {
                _path = HierarchyPath.TryCreate(PathValue).Value;
            }

            return _path;
        }

        private set
        {
            _path = value;
            PathValue = value.Value;
        }
    }

    private HierarchyPath? _path;

    /// <summary>The node's representative point. Null for areas with no coordinate yet.</summary>
    public GeoCoordinate? Center { get; private set; }

    /// <summary>
    /// The node's extent. Always consistent with <see cref="Boundary"/> when one is set, and
    /// derivable from child extents when it is not.
    /// </summary>
    public BoundingBox? Extent { get; private set; }

    /// <summary>The node's outline, when one has been imported. See known gap #2.</summary>
    public GeographicBoundary? Boundary { get; private set; }

    public PostalCode? PostalCode { get; private set; }

    /// <summary>
    /// Free-text prose for a neighbourhood hub. The frontend renders this directly, so it is
    /// stored rather than generated — a sentence assembled from a template reads like one.
    /// </summary>
    public string? Overview { get; private set; }

    /// <summary>0–100. Null until a data source exists — see known gap #1.</summary>
    public int? SafetyScore { get; private set; }

    /// <summary>0–100. Null until a data source exists.</summary>
    public int? WalkabilityScore { get; private set; }

    /// <summary>Null until a data source exists.</summary>
    public CostOfLivingBand? CostOfLiving { get; private set; }

    /// <summary>Ranks a node above its siblings in browse lists. Higher first.</summary>
    public int Prominence { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private AdministrativeArea() { }

    private AdministrativeArea(
        AreaId id,
        AdministrativeLevel level,
        PlaceName name,
        LocationSlug slug,
        CountryId countryId,
        CountryCode countryCode,
        AreaId? parentId,
        HierarchyPath path) : base(id)
    {
        Level = level;
        Name = name;
        Slug = slug;
        CountryId = countryId;
        CountryCode = countryCode;
        ParentId = parentId;
        Path = path;
    }

    /// <summary>
    /// Creates a node hanging directly off a country — a state, or a city in a country with
    /// no intervening subdivision.
    /// </summary>
    public static Result<AdministrativeArea> CreateUnderCountry(
        Countries.Country country,
        AdministrativeLevel level,
        PlaceName name,
        LocationSlug? slug = null)
    {
        ArgumentNullException.ThrowIfNull(country);
        ArgumentNullException.ThrowIfNull(name);

        if (level == AdministrativeLevel.Country)
        {
            return Result.Failure<AdministrativeArea>(Error.Validation(
                LocationErrorCodes.AreaLevelMismatch,
                "A country is registered as a Country aggregate, not as an area."));
        }

        var resolvedSlug = slug ?? LocationSlug.Create(name.Value);
        var path = country.Path.Append(resolvedSlug.Value);

        if (path.IsFailure)
        {
            return Result.Failure<AdministrativeArea>(path.Error);
        }

        return Result.Success(Emit(new AdministrativeArea(
            AreaId.New(),
            level,
            name,
            resolvedSlug,
            country.Id,
            country.Code,
            parentId: null,
            path.Value)));
    }

    /// <summary>
    /// Creates a node beneath another node.
    /// <para>
    /// The level check is the aggregate's one real invariant, and it is checked here rather
    /// than trusted to the caller because the caller is a bulk importer reading someone
    /// else's data. A city filed under a neighbourhood corrupts every ancestor query that
    /// touches it, and does so silently.
    /// </para>
    /// </summary>
    public static Result<AdministrativeArea> CreateUnder(
        AdministrativeArea parent,
        AdministrativeLevel level,
        PlaceName name,
        LocationSlug? slug = null)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(name);

        if (level <= parent.Level)
        {
            return Result.Failure<AdministrativeArea>(Error.Validation(
                LocationErrorCodes.AreaLevelMismatch,
                $"A {level} cannot sit inside a {parent.Level}."));
        }

        var resolvedSlug = slug ?? LocationSlug.Create(name.Value);
        var path = parent.Path.Append(resolvedSlug.Value);

        if (path.IsFailure)
        {
            return Result.Failure<AdministrativeArea>(path.Error);
        }

        return Result.Success(Emit(new AdministrativeArea(
            AreaId.New(),
            level,
            name,
            resolvedSlug,
            parent.CountryId,
            parent.CountryCode,
            parent.Id,
            path.Value)));
    }

    private static AdministrativeArea Emit(AdministrativeArea area)
    {
        area.RaiseDomainEvent(new AdministrativeAreaRegisteredDomainEvent
        {
            AreaId = area.Id,
            Level = area.Level,
            Name = area.Name.Value,
            CountryCode = area.CountryCode.Value,
            Path = area.Path.Value
        });

        return area;
    }

    /// <summary>How far below the root this node sits. A state is at depth 2.</summary>
    public int Depth => Path.Depth;

    /// <summary>Whether this node lies anywhere beneath <paramref name="ancestor"/>.</summary>
    public bool IsWithin(AdministrativeArea ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        return ancestor.Path.Contains(Path);
    }

    public void Rename(PlaceName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Name = name;
    }

    /// <summary>
    /// Sets the node's point and, when no boundary has been imported, its extent.
    /// <para>
    /// A boundary always wins: an extent derived from a polygon is exact, and one supplied
    /// beside a polygon could contradict it.
    /// </para>
    /// </summary>
    public void SetCenter(GeoCoordinate? center, BoundingBox? extent = null)
    {
        Center = center;

        if (Boundary is null)
        {
            Extent = extent;
        }
    }

    public void SetBoundary(GeographicBoundary? boundary)
    {
        Boundary = boundary;

        if (boundary is not null)
        {
            Extent = boundary.Extent;
        }
    }

    public void SetPostalCode(PostalCode? postalCode) => PostalCode = postalCode;

    public void SetOverview(string? overview) =>
        Overview = string.IsNullOrWhiteSpace(overview) ? null : overview.Trim();

    public void SetProminence(int prominence) => Prominence = prominence;

    /// <summary>
    /// Records the liveability indexes a neighbourhood hub renders.
    /// <para>
    /// Scores are clamped rather than rejected. They arrive from third-party imports where
    /// a 101 is a rounding artefact, and refusing the whole record over one out-of-range
    /// field would lose the other five.
    /// </para>
    /// </summary>
    public void SetQualityIndexes(int? safetyScore, int? walkabilityScore, CostOfLivingBand? costOfLiving)
    {
        SafetyScore = safetyScore is null ? null : Math.Clamp(safetyScore.Value, 0, 100);
        WalkabilityScore = walkabilityScore is null ? null : Math.Clamp(walkabilityScore.Value, 0, 100);
        CostOfLiving = costOfLiving;
    }

    /// <summary>
    /// Moves this node under a new parent and returns the path rewrite its descendants need.
    /// <para>
    /// The aggregate cannot rewrite its own descendants — they are separate aggregates, and
    /// loading them all would make a boundary redraw an unbounded transaction. It returns the
    /// old and new prefixes instead, and the application service applies them as a bounded
    /// batch. This is the cost of the materialised path, paid on the operation that happens
    /// once a decade rather than on every query.
    /// </para>
    /// </summary>
    public Result<AreaRelocation> MoveUnder(AdministrativeArea newParent)
    {
        ArgumentNullException.ThrowIfNull(newParent);

        if (newParent.Id == Id)
        {
            return Result.Failure<AreaRelocation>(Error.Validation(
                LocationErrorCodes.AreaLevelMismatch, "An area cannot contain itself."));
        }

        if (Path.Contains(newParent.Path))
        {
            return Result.Failure<AreaRelocation>(Error.Validation(
                LocationErrorCodes.AreaLevelMismatch,
                "An area cannot be moved beneath one of its own descendants."));
        }

        if (Level <= newParent.Level)
        {
            return Result.Failure<AreaRelocation>(Error.Validation(
                LocationErrorCodes.AreaLevelMismatch,
                $"A {Level} cannot sit inside a {newParent.Level}."));
        }

        var newPath = newParent.Path.Append(Slug.Value);

        if (newPath.IsFailure)
        {
            return Result.Failure<AreaRelocation>(newPath.Error);
        }

        var relocation = new AreaRelocation
        {
            AreaId = Id,
            PreviousPath = Path,
            NewPath = newPath.Value,
            NewCountryId = newParent.CountryId,
            NewCountryCode = newParent.CountryCode
        };

        ParentId = newParent.Id;
        CountryId = newParent.CountryId;
        CountryCode = newParent.CountryCode;
        Path = newPath.Value;

        RaiseDomainEvent(new AdministrativeAreaRelocatedDomainEvent
        {
            AreaId = Id,
            PreviousPath = relocation.PreviousPath.Value,
            NewPath = relocation.NewPath.Value
        });

        return Result.Success(relocation);
    }

    /// <summary>
    /// Rebases this node's path after an ancestor moved.
    /// <para>
    /// The only way a descendant's path may change, and deliberately narrow: it accepts a
    /// relocation the ancestor produced and refuses anything that is not actually beneath the
    /// moved node. A general "set the path" mutator would let a caller put a node anywhere in
    /// the tree without touching <see cref="ParentId"/>, and the two would disagree silently
    /// for as long as nobody checked.
    /// </para>
    /// </summary>
    /// <returns>True when the path changed.</returns>
    public bool ApplyAncestorRelocation(AreaRelocation relocation)
    {
        ArgumentNullException.ThrowIfNull(relocation);

        if (!Path.Value.StartsWith(relocation.PreviousDescendantPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = Path.Value[relocation.PreviousDescendantPrefix.Length..];
        var rebased = HierarchyPath.TryCreate(relocation.NewDescendantPrefix + suffix);

        if (rebased.IsFailure)
        {
            return false;
        }

        Path = rebased.Value;

        // The country can change when a node moves across a border — rare, but it happens
        // when a disputed territory is reassigned, and a stale denormalised country code is
        // worse than no denormalisation at all.
        CountryId = relocation.NewCountryId;
        CountryCode = relocation.NewCountryCode;

        return true;
    }

    /// <summary>
    /// Retires a node without erasing it.
    /// <para>
    /// Soft rather than hard, because a listing, a saved location or a member's residence may
    /// still point here. A hard delete would leave those rows referencing nothing; a retired
    /// node still renders its name, and the shared context's query filter keeps it out of
    /// every browse and search without a single call site remembering to exclude it.
    /// </para>
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

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
