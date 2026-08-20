namespace Diaspora.Modules.Conversations.Domain.Catalog;

/// <summary>The conversation context type catalogue.</summary>
public interface IConversationContextTypeRepository
{
    Task<ConversationContextType?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Every type, active and retired, ordered for display.</summary>
    Task<IReadOnlyList<ConversationContextType>> ListAsync(
        bool activeOnly = true, CancellationToken cancellationToken = default);

    void Add(ConversationContextType contextType);

    void Update(ConversationContextType contextType);
}
