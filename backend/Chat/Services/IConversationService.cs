using Chatbot.Chat.Models;

namespace Chatbot.Chat.Services;

public interface IConversationService
{
    /// <summary>
    /// Creates a new conversation with cached context
    /// </summary>
    Task<ConversationContext> CreateConversationAsync(InitializeChatRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an existing conversation by ID
    /// </summary>
    Task<ConversationContext?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates conversation with new message
    /// </summary>
    Task UpdateConversationAsync(string conversationId, string userMessage, string assistantMessage, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists all active conversations
    /// </summary>
    Task<List<ConversationListResponse>> GetConversationsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets conversation status including cache health
    /// </summary>
    Task<ConversationStatusResponse> GetConversationStatusAsync(string conversationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a conversation and its cache
    /// </summary>
    Task<bool> DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates and refreshes cache if needed
    /// </summary>
    Task<bool> ValidateAndRefreshCacheAsync(string conversationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cleanup expired conversations
    /// </summary>
    Task CleanupExpiredConversationsAsync(CancellationToken cancellationToken = default);
}