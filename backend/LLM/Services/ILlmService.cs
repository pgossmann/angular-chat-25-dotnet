using Chatbot.LLM.Models;

namespace Chatbot.LLM.Services;

public interface ILlmService
{
    /// <summary>
    /// Gets a non-streaming response from the LLM
    /// </summary>
    Task<LlmResponse> GetCompletionAsync(LlmRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a streaming response from the LLM
    /// </summary>
    IAsyncEnumerable<string> GetStreamingCompletionAsync(LlmRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the name of the LLM provider
    /// </summary>
    string ProviderName { get; }
    
    /// <summary>
    /// Gets the supported models for this provider
    /// </summary>
    IEnumerable<string> SupportedModels { get; }
    
    /// <summary>
    /// Checks if the service is properly configured and can be used
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Caches text content in the LLM for efficient reuse
    /// </summary>
    Task<string> CacheTextContextAsync(string context, string systemPrompt, string model, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Caches file content in the LLM for efficient reuse
    /// </summary>
    Task<string> CacheFileContextAsync(byte[] fileContent, string fileName, string mimeType, string systemPrompt, string model, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a streaming response using cached context
    /// </summary>
    IAsyncEnumerable<string> GetStreamingCompletionWithCacheAsync(string cacheId, string userMessage, List<ChatMessage> chatHistory, LlmSettings settings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates if a cache is still valid and accessible
    /// </summary>
    Task<bool> ValidateCacheAsync(string cacheId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a cache from the LLM provider
    /// </summary>
    Task<bool> DeleteCacheAsync(string cacheId, CancellationToken cancellationToken = default);
}