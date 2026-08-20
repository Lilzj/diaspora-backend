using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// Who the property is for, expressed as the flags a search actually filters on.
/// <para>
/// <b>Flags rather than a single audience enum, because they genuinely overlap.</b> A large flat
/// near a university is student-friendly and family-friendly at once; a serviced apartment takes
/// short stays and long ones. An enum would force the landlord to pick one and would hide the
/// property from half the people it suits.
/// </para>
/// <para>
/// <b>The one invariant is that a property must accept some length of stay.</b> A listing that is
/// neither short-stay nor long-stay is one nobody can ever book, and it is far better to refuse it
/// at creation than to let a landlord wonder for a fortnight why no one is calling.
/// </para>
/// <para>
/// <b><see cref="IsSharedAccommodation"/> is the gate on roommate matching and on gender
/// preference</b>, and it is stored here rather than derived from the accommodation type because
/// the type catalogue is configurable: a deployment may add "Co-living Suite" and Housing must not
/// have to learn the name to know that a preference is permitted on it.
/// </para>
/// </summary>
public sealed class StayPolicy : ValueObject
{
    private StayPolicy(
        bool acceptsShortStay,
        bool acceptsLongStay,
        bool isStudentFriendly,
        bool isFamilyFriendly,
        bool isSharedAccommodation,
        bool isLookingForRoommate)
    {
        AcceptsShortStay = acceptsShortStay;
        AcceptsLongStay = acceptsLongStay;
        IsStudentFriendly = isStudentFriendly;
        IsFamilyFriendly = isFamilyFriendly;
        IsSharedAccommodation = isSharedAccommodation;
        IsLookingForRoommate = isLookingForRoommate;
    }

    private StayPolicy() { }

    /// <summary>Temporary accommodation. What somebody needs for the six weeks before they settle.</summary>
    public bool AcceptsShortStay { get; private set; }

    public bool AcceptsLongStay { get; private set; }

    public bool IsStudentFriendly { get; private set; }

    public bool IsFamilyFriendly { get; private set; }

    /// <summary>
    /// Whether somebody else lives there. The gate on roommate preferences and, more consequently,
    /// on gender preference — see <see cref="Enums.GenderPreference"/>.
    /// </summary>
    public bool IsSharedAccommodation { get; private set; }

    /// <summary>An open room in an existing household, actively looking.</summary>
    public bool IsLookingForRoommate { get; private set; }

    /// <summary>
    /// A long-let whole property, which is what most listings are.
    /// <para>
    /// A fresh instance per access: this is an owned type, and a shared static one makes EF write
    /// nulls into a second aggregate holding it.
    /// </para>
    /// </summary>
    public static StayPolicy LongLet => new(false, true, false, false, false, false);

    public static Result<StayPolicy> Create(
        bool acceptsShortStay,
        bool acceptsLongStay,
        bool isStudentFriendly = false,
        bool isFamilyFriendly = false,
        bool isSharedAccommodation = false,
        bool isLookingForRoommate = false)
    {
        if (!acceptsShortStay && !acceptsLongStay)
        {
            return Result.Failure<StayPolicy>(HousingErrors.StayPolicyInvalid);
        }

        // Looking for a roommate implies the accommodation is shared. Inferring it rather than
        // refusing it: the two flags say the same thing from different angles, and a landlord who
        // ticked one plainly meant both.
        return Result.Success(new StayPolicy(
            acceptsShortStay,
            acceptsLongStay,
            isStudentFriendly,
            isFamilyFriendly,
            isSharedAccommodation || isLookingForRoommate,
            isLookingForRoommate));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AcceptsShortStay;
        yield return AcceptsLongStay;
        yield return IsStudentFriendly;
        yield return IsFamilyFriendly;
        yield return IsSharedAccommodation;
        yield return IsLookingForRoommate;
    }
}
