using System.Collections.Concurrent;
using Chatbot.Chat.Models;
using Chatbot.LLM.Services;
using Chatbot.LLM.Models;

namespace Chatbot.Chat.Services;

public class ConversationService : IConversationService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<ConversationService> _logger;
    private readonly ConcurrentDictionary<string, ConversationContext> _conversations = new();
    
    // File validation constants
    private const long MaxFileSize = 10 * 1024 * 1024; // 10MB
    private static readonly string[] AllowedMimeTypes = { "application/pdf", "text/plain", "text/markdown" };

    public ConversationService(ILlmService llmService, ILogger<ConversationService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ConversationContext> CreateConversationAsync(InitializeChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var conversation = new ConversationContext
            {
                SystemPrompt = request.SystemPrompt,
                Settings = request.Settings,
                OriginalContext = request.Context
            };

            string cacheId;

            // Handle file upload if present
            if (request.File != null)
            {
                ValidateFile(request.File);
                
                using var memoryStream = new MemoryStream();
                await request.File.CopyToAsync(memoryStream, cancellationToken);
                var fileContent = memoryStream.ToArray();

                conversation.Document = new CachedDocument
                {
                    Filename = request.File.FileName,
                    ContentType = request.File.ContentType,
                    FileSize = request.File.Length,
                    OriginalContent = fileContent
                };

                cacheId = await _llmService.CacheFileContextAsync(
                    fileContent, 
                    request.File.FileName, 
                    request.File.ContentType, 
                    request.SystemPrompt, 
                    request.Settings.Model, 
                    cancellationToken);

                conversation.Document.CacheId = cacheId;
            }
            else if (!string.IsNullOrWhiteSpace(request.Context))
            {
                // Cache text context
                cacheId = await _llmService.CacheTextContextAsync(
                    request.Context, 
                    request.SystemPrompt, 
                    request.Settings.Model, 
                    cancellationToken);
            }
            else
            {
                throw new ArgumentException("Either context text or file must be provided");
            }

            conversation.CacheId = cacheId;
            conversation.ExpiryAt = DateTime.UtcNow.AddHours(1); // 1 hour expiry

            // Add first message if provided
            if (!string.IsNullOrEmpty(request.FirstMessage))
            {
                conversation.ChatHistory.Add(new ChatMessage
                {
                    Role = "user",
                    Content = request.FirstMessage,
                    Timestamp = DateTime.UtcNow
                });
            }

            _conversations.TryAdd(conversation.Id, conversation);
            
            _logger.LogInformation("Created conversation {ConversationId} with cache {CacheId}", 
                conversation.Id, cacheId);

            return conversation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating conversation");
            throw;
        }
    }

    public async Task<ConversationContext?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            // Check if conversation is expired
            if (conversation.ExpiryAt.HasValue && conversation.ExpiryAt.Value <= DateTime.UtcNow)
            {
                _logger.LogInformation("Conversation {ConversationId} has expired", conversationId);
                await DeleteConversationAsync(conversationId, cancellationToken);
                return null;
            }

            return conversation;
        }

        return null;
    }

    public Task UpdateConversationAsync(string conversationId, string userMessage, string assistantMessage, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryGetValue(conversationId, out var conversation))
        {
            conversation.ChatHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage,
                Timestamp = DateTime.UtcNow
            });

            conversation.ChatHistory.Add(new ChatMessage
            {
                Role = "assistant",
                Content = assistantMessage,
                Timestamp = DateTime.UtcNow
            });

            _logger.LogDebug("Updated conversation {ConversationId} with new messages", conversationId);
        }
        else
        {
            _logger.LogWarning("Attempted to update non-existent conversation {ConversationId}", conversationId);
        }

        return Task.CompletedTask;
    }

    public Task<List<ConversationListResponse>> GetConversationsAsync(CancellationToken cancellationToken = default)
    {
        var conversations = new List<ConversationListResponse>();

        foreach (var kvp in _conversations)
        {
            var conversation = kvp.Value;
            var isActive = !conversation.ExpiryAt.HasValue || conversation.ExpiryAt.Value > DateTime.UtcNow;

            conversations.Add(new ConversationListResponse
            {
                Id = conversation.Id,
                CreatedAt = conversation.CreatedAt,
                ExpiryAt = conversation.ExpiryAt,
                MessageCount = conversation.ChatHistory.Count,
                DocumentName = conversation.Document?.Filename,
                IsActive = isActive
            });
        }

        return Task.FromResult(conversations.OrderByDescending(c => c.CreatedAt).ToList());
    }

    public async Task<ConversationStatusResponse> GetConversationStatusAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            throw new ArgumentException($"Conversation {conversationId} not found");
        }

        var isActive = !conversation.ExpiryAt.HasValue || conversation.ExpiryAt.Value > DateTime.UtcNow;
        var cacheValid = await _llmService.ValidateCacheAsync(conversation.CacheId, cancellationToken);

        return new ConversationStatusResponse
        {
            ConversationId = conversationId,
            IsActive = isActive,
            CacheValid = cacheValid,
            CacheExpiryAt = conversation.ExpiryAt,
            MessageCount = conversation.ChatHistory.Count,
            DocumentName = conversation.Document?.Filename
        };
    }

    public async Task<bool> DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (_conversations.TryRemove(conversationId, out var conversation))
        {
            // Delete cache from LLM provider
            await _llmService.DeleteCacheAsync(conversation.CacheId, cancellationToken);
            
            _logger.LogInformation("Deleted conversation {ConversationId} and cache {CacheId}", 
                conversationId, conversation.CacheId);
            return true;
        }

        return false;
    }

    public async Task<bool> ValidateAndRefreshCacheAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (!_conversations.TryGetValue(conversationId, out var conversation))
        {
            return false;
        }

        var cacheValid = await _llmService.ValidateCacheAsync(conversation.CacheId, cancellationToken);
        
        if (!cacheValid)
        {
            _logger.LogInformation("Cache {CacheId} is invalid, attempting refresh for conversation {ConversationId}", 
                conversation.CacheId, conversationId);

            try
            {
                string newCacheId;

                // Re-create cache based on original data
                if (conversation.Document != null)
                {
                    newCacheId = await _llmService.CacheFileContextAsync(
                        conversation.Document.OriginalContent,
                        conversation.Document.Filename,
                        conversation.Document.ContentType,
                        conversation.SystemPrompt,
                        conversation.Settings.Model,
                        cancellationToken);
                }
                else
                {
                    newCacheId = await _llmService.CacheTextContextAsync(
                        conversation.OriginalContext,
                        conversation.SystemPrompt,
                        conversation.Settings.Model,
                        cancellationToken);
                }

                // Update conversation with new cache ID
                conversation.CacheId = newCacheId;
                conversation.ExpiryAt = DateTime.UtcNow.AddHours(1);

                _logger.LogInformation("Successfully refreshed cache for conversation {ConversationId}, new cache: {CacheId}", 
                    conversationId, newCacheId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh cache for conversation {ConversationId}", conversationId);
                return false;
            }
        }

        return true;
    }

    public async Task CleanupExpiredConversationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredConversations = _conversations.Values
            .Where(c => c.ExpiryAt.HasValue && c.ExpiryAt.Value <= DateTime.UtcNow)
            .ToList();

        foreach (var conversation in expiredConversations)
        {
            await DeleteConversationAsync(conversation.Id, cancellationToken);
        }

        if (expiredConversations.Any())
        {
            _logger.LogInformation("Cleaned up {Count} expired conversations", expiredConversations.Count);
        }
    }

    private static void ValidateFile(IFormFile file)
    {
        if (file.Length > MaxFileSize)
        {
            throw new ArgumentException($"File size exceeds maximum allowed size of {MaxFileSize / 1024 / 1024}MB");
        }

        if (!AllowedMimeTypes.Contains(file.ContentType))
        {
            throw new ArgumentException($"File type {file.ContentType} is not supported. Allowed types: {string.Join(", ", AllowedMimeTypes)}");
        }

        if (string.IsNullOrEmpty(file.FileName))
        {
            throw new ArgumentException("File name is required");
        }
    }
}