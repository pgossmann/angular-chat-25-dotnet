using Chatbot.Chat.Models;
using MediatR;

namespace Chatbot.Chat.Commands;

public class SendChatMessageCommand : IRequest<ChatResponse>
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<LLM.Models.ChatMessage> History { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public ChatSettings Settings { get; set; } = new();
}

public class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, ChatResponse>
{
    private readonly LLM.Services.ILlmService _llmService;
    private readonly ILogger<SendChatMessageCommandHandler> _logger;

    public SendChatMessageCommandHandler(LLM.Services.ILlmService llmService, ILogger<SendChatMessageCommandHandler> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<ChatResponse> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing chat message: {Message}", request.Message[..Math.Min(50, request.Message.Length)]);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            throw;
        }
    }
}