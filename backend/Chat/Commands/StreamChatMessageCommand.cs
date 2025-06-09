using Chatbot.Chat.Models;
using MediatR;

namespace Chatbot.Chat.Commands;

public class StreamChatMessageCommand : IRequest<IAsyncEnumerable<StreamingChatResponse>>
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<LLM.Models.ChatMessage> History { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public ChatSettings Settings { get; set; } = new();
}

public class StreamChatMessageCommandHandler : IRequestHandler<StreamChatMessageCommand, IAsyncEnumerable<StreamingChatResponse>>
{
    private readonly LLM.Services.ILlmService _llmService;
    private readonly ILogger<StreamChatMessageCommandHandler> _logger;

    public StreamChatMessageCommandHandler(LLM.Services.ILlmService llmService, ILogger<StreamChatMessageCommandHandler> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public Task<IAsyncEnumerable<StreamingChatResponse>> Handle(StreamChatMessageCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing streaming chat message: {Message}", request.Message[..Math.Min(50, request.Message.Length)]);

        var llmRequest = new LLM.Models.LlmRequest
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

        var messageId = Guid.NewGuid().ToString();
        return Task.FromResult(StreamResponseAsync(llmRequest, messageId, cancellationToken));
    }

    private async IAsyncEnumerable<StreamingChatResponse> StreamResponseAsync(
        LLM.Models.LlmRequest llmRequest, 
        string messageId, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool hasContent = false;
        bool hasError = false;

        // Stream the LLM responses
        await foreach (var chunk in SafeStreamLlmAsync(llmRequest, cancellationToken))
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

        // Send completion or error marker
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

    private async IAsyncEnumerable<StreamChunk> SafeStreamLlmAsync(
        LLM.Models.LlmRequest llmRequest,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<string>? enumerator = null;
        Exception? exception = null;
        
        try
        {
            enumerator = _llmService.GetStreamingCompletionAsync(llmRequest, cancellationToken).GetAsyncEnumerator(cancellationToken);
            
            while (true)
            {
                bool hasNext;
                string? chunk = null;
                
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                    if (hasNext)
                        chunk = enumerator.Current;
                }
                catch (Exception ex)
                {
                    exception = ex;
                    break;
                }
                
                if (!hasNext)
                    break;
                    
                if (chunk != null)
                {
                    yield return new StreamChunk { Content = chunk, IsError = false };
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
}