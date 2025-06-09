using Chatbot.Chat.Models;
using Chatbot.Chat.Services;
using Chatbot.LLM.Services;
using Chatbot.LLM.Models;
using MediatR;

namespace Chatbot.Chat.Commands;

public class StreamCachedChatMessageCommand : IRequest<IAsyncEnumerable<StreamingChatResponse>>
{
    public string ConversationId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public ChatSettings? Settings { get; set; }
}

public class StreamCachedChatMessageCommandHandler : IRequestHandler<StreamCachedChatMessageCommand, IAsyncEnumerable<StreamingChatResponse>>
{
    private readonly IConversationService _conversationService;
    private readonly ILlmService _llmService;
    private readonly ILogger<StreamCachedChatMessageCommandHandler> _logger;

    public StreamCachedChatMessageCommandHandler(
        IConversationService conversationService,
        ILlmService llmService,
        ILogger<StreamCachedChatMessageCommandHandler> logger)
    {
        _conversationService = conversationService;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<IAsyncEnumerable<StreamingChatResponse>> Handle(StreamCachedChatMessageCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing streaming cached chat message for conversation: {ConversationId}", request.ConversationId);

        var conversation = await _conversationService.GetConversationAsync(request.ConversationId, cancellationToken);
        if (conversation == null)
        {
            throw new ArgumentException($"Conversation {request.ConversationId} not found or expired");
        }

        // Validate and refresh cache if needed
        var cacheValid = await _conversationService.ValidateAndRefreshCacheAsync(request.ConversationId, cancellationToken);
        if (!cacheValid)
        {
            throw new InvalidOperationException($"Cache for conversation {request.ConversationId} is invalid and could not be refreshed");
        }

        // Use settings from request or conversation
        var settings = request.Settings ?? conversation.Settings;

        var llmSettings = new LlmSettings
        {
            Temperature = settings.Temperature,
            MaxTokens = settings.MaxTokens,
            Model = settings.Model
        };

        var messageId = Guid.NewGuid().ToString();
        return StreamResponseAsync(conversation, request.UserMessage, llmSettings, messageId, cancellationToken);
    }

    private async IAsyncEnumerable<StreamingChatResponse> StreamResponseAsync(
        ConversationContext conversation,
        string userMessage,
        LlmSettings settings,
        string messageId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        bool hasContent = false;
        bool hasError = false;
        var fullContent = string.Empty;

        // Stream the LLM responses using cached context
        await foreach (var chunk in SafeStreamCachedLlmAsync(conversation, userMessage, settings, cancellationToken))
        {
            if (chunk.IsError)
            {
                hasError = true;
                _logger.LogError("Error during cached streaming response: {Error}", chunk.ErrorMessage);
                break;
            }

            hasContent = true;
            fullContent += chunk.Content;
            
            yield return new StreamingChatResponse
            {
                Content = chunk.Content,
                IsComplete = false,
                MessageId = messageId
            };
        }

        // Update conversation with messages after streaming completes
        if (hasContent && !hasError)
        {
            await _conversationService.UpdateConversationAsync(
                conversation.Id,
                userMessage,
                fullContent,
                cancellationToken);
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

    private async IAsyncEnumerable<StreamChunk> SafeStreamCachedLlmAsync(
        ConversationContext conversation,
        string userMessage,
        LlmSettings settings,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IAsyncEnumerator<string>? enumerator = null;
        Exception? exception = null;

        try
        {
            enumerator = _llmService.GetStreamingCompletionWithCacheAsync(
                conversation.CacheId,
                userMessage,
                conversation.ChatHistory,
                settings,
                cancellationToken).GetAsyncEnumerator(cancellationToken);

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