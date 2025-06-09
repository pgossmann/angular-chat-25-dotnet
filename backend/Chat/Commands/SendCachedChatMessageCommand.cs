using Chatbot.Chat.Models;
using Chatbot.Chat.Services;
using Chatbot.LLM.Services;
using Chatbot.LLM.Models;
using MediatR;

namespace Chatbot.Chat.Commands;

public class SendCachedChatMessageCommand : IRequest<ChatResponse>
{
    public string ConversationId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public ChatSettings? Settings { get; set; }
}

public class SendCachedChatMessageCommandHandler : IRequestHandler<SendCachedChatMessageCommand, ChatResponse>
{
    private readonly IConversationService _conversationService;
    private readonly ILlmService _llmService;
    private readonly ILogger<SendCachedChatMessageCommandHandler> _logger;

    public SendCachedChatMessageCommandHandler(
        IConversationService conversationService,
        ILlmService llmService,
        ILogger<SendCachedChatMessageCommandHandler> logger)
    {
        _conversationService = conversationService;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ChatResponse> Handle(SendCachedChatMessageCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing cached chat message for conversation: {ConversationId}", request.ConversationId);

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

        // Get response using cached context
        var llmRequest = new LlmRequest
        {
            SystemPrompt = conversation.SystemPrompt,
            UserMessage = request.UserMessage,
            ChatHistory = conversation.ChatHistory,
            Settings = llmSettings
        };

        var response = await _llmService.GetCompletionAsync(llmRequest, cancellationToken);

        // Update conversation with new messages
        await _conversationService.UpdateConversationAsync(
            request.ConversationId,
            request.UserMessage,
            response.Content,
            cancellationToken);

        return new ChatResponse
        {
            Message = response.Content,
            Id = response.Id,
            Timestamp = response.Timestamp,
            Usage = response.Usage != null ? new ChatUsage
            {
                PromptTokens = response.Usage.PromptTokens,
                CompletionTokens = response.Usage.CompletionTokens,
                TotalTokens = response.Usage.TotalTokens
            } : null
        };
    }
}