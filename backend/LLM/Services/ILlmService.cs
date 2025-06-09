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
}