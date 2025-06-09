using Chatbot.Chat.Models;
using Chatbot.Chat.Services;
using Chatbot.LLM.Services;
using Chatbot.LLM.Models;
using MediatR;

namespace Chatbot.Chat.Commands;

public class InitializeChatCommand : IRequest<InitializeChatResponse>
{
    public string Context { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string FirstMessage { get; set; } = string.Empty;
    public ChatSettings Settings { get; set; } = new();
}

public class InitializeChatCommandHandler : IRequestHandler<InitializeChatCommand, InitializeChatResponse>
{
    private readonly IConversationService _conversationService;
    private readonly ILlmService _llmService;
    private readonly ILogger<InitializeChatCommandHandler> _logger;

    public InitializeChatCommandHandler(
        IConversationService conversationService,
        ILlmService llmService,
        ILogger<InitializeChatCommandHandler> logger)
    {
        _conversationService = conversationService;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<InitializeChatResponse> Handle(InitializeChatCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing chat with context caching");

        var initRequest = new InitializeChatRequest
        {
            Context = request.Context,
            File = request.File,
            SystemPrompt = request.SystemPrompt,
            FirstMessage = request.FirstMessage,
            Settings = request.Settings
        };

        // Create conversation with cached context
        var conversation = await _conversationService.CreateConversationAsync(initRequest, cancellationToken);

        string responseMessage = "Context cached successfully.";
        string messageId = Guid.NewGuid().ToString();

        // If first message is provided, get response
        if (!string.IsNullOrEmpty(request.FirstMessage))
        {
            var responseContent = await GetFirstResponseAsync(conversation, request.FirstMessage, cancellationToken);
            responseMessage = responseContent;

            // Update conversation with assistant response
            await _conversationService.UpdateConversationAsync(
                conversation.Id, 
                request.FirstMessage, 
                responseContent, 
                cancellationToken);
        }

        return new InitializeChatResponse
        {
            ConversationId = conversation.Id,
            Message = responseMessage,
            MessageId = messageId,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<string> GetFirstResponseAsync(ConversationContext conversation, string firstMessage, CancellationToken cancellationToken)
    {
        try
        {
            var llmRequest = new LlmRequest
            {
                SystemPrompt = conversation.SystemPrompt,
                UserMessage = firstMessage,
                ChatHistory = new List<ChatMessage>(),
                Settings = new LlmSettings
                {
                    Temperature = conversation.Settings.Temperature,
                    MaxTokens = conversation.Settings.MaxTokens,
                    Model = conversation.Settings.Model
                }
            };

            // For the first message, we'll use regular completion since the context is already cached
            var response = await _llmService.GetCompletionAsync(llmRequest, cancellationToken);
            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting first response for conversation {ConversationId}", conversation.Id);
            return "I'm ready to help! Please send your first message.";
        }
    }
}