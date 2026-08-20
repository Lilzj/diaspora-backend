using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// What a conversation is about, as a catalogue key.
/// <para>
/// <b>A string key checked against reference data, not an enum, and that is the phase's "never
/// hardcode vertical logic" rule expressed in one type.</b> An enum would put
/// <c>MarketplaceListing</c>, <c>HousingListing</c>, <c>JobOpportunity</c> in this module's source,
/// which means the day somebody adds a Tutoring vertical the Conversations module needs a code
/// change, a migration and a deployment to let two people talk about a tutor. With a catalogue, the
/// new vertical contributes a seed through
/// <see cref="Extensibility.IConversationContextTypeContributor"/> and this module never
/// learns the vertical exists.
/// </para>
/// <para>
/// <b>The keys are the client's literals.</b> <c>MarketplaceListing</c>, not
/// <c>marketplace_listing</c>; the twelve values in <c>src/types/conversation.ts</c> spelled exactly
/// as they are spelled there, because the client sends them and switches on them. Comparison is
/// ordinal and case-sensitive for the same reason — a key that matched case-insensitively would let
/// two spellings of one type coexist in the catalogue and in the filter.
/// </para>
/// </summary>
public sealed class ConversationType : ValueObject
{
    public const int MaxLength = 64;

    private ConversationType(string key) => Key = key;

    private ConversationType() { }

    public string Key { get; private set; } = default!;

    /// <summary>
    /// Wraps a key that has already been validated against the catalogue.
    /// <para>
    /// Deliberately does not consult the catalogue itself: a value object that needed a repository
    /// could not be constructed in a unit test, and the check belongs at the one place a caller
    /// supplies a type — see <c>ConversationContextTypeCatalog</c>.
    /// </para>
    /// </summary>
    public static ConversationType From(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var trimmed = key.Trim();

        return new ConversationType(
            trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength]);
    }

    public bool Is(string key) => string.Equals(Key, key, StringComparison.Ordinal);

    public override string ToString() => Key;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
    }
}

/// <summary>
/// The twelve context types the client ships with, as constants.
/// <para>
/// Here so this module's own seed contributor and its tests can name them without a magic string.
/// <b>Nothing in the domain switches on these.</b> They are catalogue keys that happen to be known
/// at compile time, not a closed set — an architecture test asserts no handler branches on one.
/// </para>
/// </summary>
public static class ConversationTypeKeys
{
    public const string ProviderInquiry = "ProviderInquiry";
    public const string MarketplaceListing = "MarketplaceListing";
    public const string HousingListing = "HousingListing";
    public const string JobOpportunity = "JobOpportunity";
    public const string CommunityEvent = "CommunityEvent";
    public const string Organization = "Organization";
    public const string CommunityGuide = "CommunityGuide";
    public const string SupportTicket = "SupportTicket";
    public const string TrustAppeal = "TrustAppeal";
    public const string PrivateConversation = "PrivateConversation";
    public const string AiAssistant = "AIAssistant";
    public const string BusinessAccount = "BusinessAccount";
}
