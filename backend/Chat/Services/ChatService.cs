using Chatbot.Chat.Models;
using Chatbot.LLM.Services;

namespace Chatbot.Chat.Services;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<StreamingChatResponse> StreamMessageAsync(ChatRequest request, CancellationToken cancellationToken = default);
    Task<bool> ValidateRequestAsync(ChatRequest request);
}

public class ChatService : IChatService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<ChatService> _logger;
    
    private const int MaxSystemPromptLength = 2000;
    private const int MaxMessageLength = 10000;
    private const int MaxHistoryCount = 50;

    public ChatService(ILlmService llmService, ILogger<ChatService> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ChatResponse> SendMessageAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (!await ValidateRequestAsync(request))
        {
            throw new ArgumentException("Invalid chat request");
        }

        var llmRequest = MapToLlmRequest(request);
        var llmResponse = await _llmService.GetCompletionAsync(llmRequest, cancellationToken);

        return new ChatResponse
        {
            Message = llmResponse.Content,
            Id = llmResponse.Id,
            Timestamp = llmResponse.Timestamp,
            Usage = llmResponse.Usage != null ? new ChatUsage
            {
                PromptTokens = llmResponse.Usage.PromptTokens,
                CompletionTokens = llmResponse.Usage.CompletionTokens,
                TotalTokens = llmResponse.Usage.TotalTokens
            } : null
        };
    }

    public async IAsyncEnumerable<StreamingChatResponse> StreamMessageAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!await ValidateRequestAsync(request))
        {
            yield return new StreamingChatResponse
            {
                Content = "Error: Invalid chat request",
                IsComplete = true,
                MessageId = Guid.NewGuid().ToString()
            };
            yield break;
        }

        var llmRequest = MapToLlmRequest(request);
        var messageId = Guid.NewGuid().ToString();

        await foreach (var response in GetStreamingResponseAsync(llmRequest, messageId, cancellationToken))
        {
            yield return response;
        }
    }

    private async IAsyncEnumerable<StreamingChatResponse> GetStreamingResponseAsync(
        LLM.Models.LlmRequest llmRequest, 
        string messageId, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        bool hasContent = false;
        bool hasError = false;

        // Stream the LLM responses
        var llmStream = _llmService.GetStreamingCompletionAsync(llmRequest, cancellationToken);
        
        await foreach (var chunk in SafeStreamAsync(llmStream, cancellationToken))
        {
            if (chunk.IsError)
            {
                hasError = true;
                _logger.LogError("Error during streaming response: {Error}", chunk.ErrorMessage);
                break;
            }
            
            hasContent = true;
            yield return new StreamingChatResponse
            {
                Content = chunk.Content,
                IsComplete = false,
                MessageId = messageId
            };
        }

        // Send completion or error response
        if (hasError)
        {
            yield return new StreamingChatResponse
            {
                Content = "Error: Failed to generate response",
                IsComplete = true,
                MessageId = messageId
            };
        }
        else if (hasContent)
        {
            yield return new StreamingChatResponse
            {
                Content = string.Empty,
                IsComplete = true,
                MessageId = messageId
            };
        }
    }

    private async IAsyncEnumerable<StreamChunk> SafeStreamAsync(
        IAsyncEnumerable<string> source,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<string>? enumerator = null;
        Exception? exception = null;
        
        try
        {
            enumerator = source.GetAsyncEnumerator(cancellationToken);
            
            while (true)
            {
                bool hasNext;
                string? item = null;
                
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                        item = enumerator.Current;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    break;
                }
                
                if (!hasNext)
                    break;
                    
                if (item != null)
                {
                    yield return new StreamChunk { Content = item, IsError = false };
                }
            }
        }
        finally
        {
            if (enumerator != null)
            {
                await enumerator.DisposeAsync();
            }
        }

        if (exception != null)
        {
            yield return new StreamChunk { IsError = true, ErrorMessage = exception.Message };
        }
    }

    private class StreamChunk
    {
        public string Content { get; set; } = string.Empty;
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public Task<bool> ValidateRequestAsync(ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("Chat request validation failed: Empty message");
            return Task.FromResult(false);
        }

        if (request.Message.Length > MaxMessageLength)
        {
            _logger.LogWarning("Chat request validation failed: Message too long ({Length})", request.Message.Length);
            return Task.FromResult(false);
        }

        if (request.SystemPrompt.Length > MaxSystemPromptLength)
        {
            _logger.LogWarning("Chat request validation failed: System prompt too long ({Length})", request.SystemPrompt.Length);
            return Task.FromResult(false);
        }

        if (request.History.Count > MaxHistoryCount)
        {
            _logger.LogWarning("Chat request validation failed: Too many history messages ({Count})", request.History.Count);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    private LLM.Models.LlmRequest MapToLlmRequest(ChatRequest request)
    {
        return new LLM.Models.LlmRequest
        {
            SystemPrompt = request.SystemPrompt,
            Context = request.Context,
            ChatHistory = request.History,
            UserMessage = request.Message,
            Settings = new LLM.Models.LlmSettings
            {
                Temperature = request.Settings.Temperature,
                MaxTokens = request.Settings.MaxTokens,
                Model = request.Settings.Model
            }
        };
    }
}