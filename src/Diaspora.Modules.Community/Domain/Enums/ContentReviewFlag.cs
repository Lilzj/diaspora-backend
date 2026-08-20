namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// What an automated review objected to.
/// <para>
/// Names taken from the client, which spells them in lower snake_case while spelling every
/// sibling enum in PascalCase. B6 emits camelCase like every other platform enum and accepts
/// the client snake_case spelling on input, which is the general rule the frontend contract
/// report records rather than a special case for this type.
/// </para>
/// </summary>
public enum ContentReviewFlag
{
    Spam = 0,

    HateSpeech = 1,

    OffensiveLanguage = 2,

    Duplicate = 3,

    PersonalData = 4,

    Copyright = 5,

    LowQuality = 6
}
