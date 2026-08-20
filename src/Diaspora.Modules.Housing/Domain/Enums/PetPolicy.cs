namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>Whether pets are permitted, and which.</summary>
public enum PetPolicy
{
    Unspecified = 0,
    NotAllowed = 1,
    CatsOnly = 2,
    DogsOnly = 3,
    Negotiable = 4,
    Allowed = 5
}
