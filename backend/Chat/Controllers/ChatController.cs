using Microsoft.AspNetCore.Mvc;
using MediatR;
using Chatbot.Chat.Commands;
using Chatbot.Chat.Models;
using Chatbot.Chat.Services;
using System.Text.Json;
using System.Text;

namespace Chatbot.Chat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatController> _logger;
    private readonly IConversationService _conversationService;

    public ChatController(IMediator mediator, ILogger<ChatController> logger, IConversationService conversationService)
    {
        _mediator = mediator;
        _logger = logger;
        _conversationService = conversationService;
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Received chat message request");

            var command = new SendChatMessageCommand
            {
                SystemPrompt = request.SystemPrompt,
                Context = request.Context,
                History = request.History,
                Message = request.Message,
                Settings = request.Settings
            };

            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid chat request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("stream")]
    public async Task StreamMessage([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Received streaming chat message request");

            // Set up Server-Sent Events headers
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["Access-Control-Allow-Origin"] = "*";

            var command = new StreamChatMessageCommand
            {
                SystemPrompt = request.SystemPrompt,
                Context = request.Context,
                History = request.History,
                Message = request.Message,
                Settings = request.Settings
            };

            var streamingResponse = await _mediator.Send(command, cancellationToken);

            await foreach (var chunk in streamingResponse.WithCancellation(cancellationToken))
            {
                var data = JsonSerializer.Serialize(chunk);
                await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                if (chunk.IsComplete)
                {
                    break;
                }
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid streaming chat request");
            var errorData = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {errorData}\n\n", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming chat message");
            var errorData = JsonSerializer.Serialize(new { error = "Internal server error" });
            await Response.WriteAsync($"data: {errorData}\n\n", cancellationToken);
        }
    }

    [HttpPost("initialize")]
    public async Task<ActionResult<InitializeChatResponse>> InitializeChat([FromForm] InitializeChatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Received chat initialization request");

            // Custom validation: Either context or file must be provided
            if (string.IsNullOrWhiteSpace(request.Context) && request.File == null)
            {
                return BadRequest(new { error = "Either context text or file must be provided for chat initialization." });
            }

            var command = new InitializeChatCommand
            {
                Context = request.Context,
                File = request.File,
                SystemPrompt = request.SystemPrompt,
                FirstMessage = request.FirstMessage,
                Settings = request.Settings
            };

            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid chat initialization request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing chat");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> SendCachedMessage([FromBody] ChatMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Received cached chat message request for conversation: {ConversationId}", request.ConversationId);

            var command = new SendCachedChatMessageCommand
            {
                ConversationId = request.ConversationId,
                UserMessage = request.UserMessage,
                Settings = request.Settings
            };

            var response = await _mediator.Send(command, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid cached chat message request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing cached chat message");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpPost("message/stream")]
    public async Task StreamCachedMessage([FromBody] ChatMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Received streaming cached chat message request for conversation: {ConversationId}", request.ConversationId);

            // Set up Server-Sent Events headers
            Response.Headers["Content-Type"] = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["Access-Control-Allow-Origin"] = "*";

            var command = new StreamCachedChatMessageCommand
            {
                ConversationId = request.ConversationId,
                UserMessage = request.UserMessage,
                Settings = request.Settings
            };

            var streamingResponse = await _mediator.Send(command, cancellationToken);

            await foreach (var chunk in streamingResponse.WithCancellation(cancellationToken))
            {
                var data = JsonSerializer.Serialize(chunk);
                await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                if (chunk.IsComplete)
                {
                    break;
                }
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid streaming cached chat request");
            var errorData = JsonSerializer.Serialize(new { error = ex.Message });
            await Response.WriteAsync($"data: {errorData}\n\n", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing streaming cached chat message");
            var errorData = JsonSerializer.Serialize(new { error = "Internal server error" });
            await Response.WriteAsync($"data: {errorData}\n\n", cancellationToken);
        }
    }

    [HttpGet("conversations")]
    public async Task<ActionResult<List<ConversationListResponse>>> GetConversations(CancellationToken cancellationToken)
    {
        try
        {
            var conversations = await _conversationService.GetConversationsAsync(cancellationToken);
            return Ok(conversations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("conversation/{id}/status")]
    public async Task<ActionResult<ConversationStatusResponse>> GetConversationStatus(string id, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _conversationService.GetConversationStatusAsync(id, cancellationToken);
            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid conversation status request");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation status");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpDelete("conversation/{id}")]
    public async Task<ActionResult> DeleteConversation(string id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _conversationService.DeleteConversationAsync(id, cancellationToken);
            if (deleted)
            {
                return NoContent();
            }
            return NotFound(new { error = "Conversation not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("hello")]
    public IActionResult HelloWorld()
    {
        return Ok(new { message = "Hello World from ASP.NET Core Chatbot!", timestamp = DateTime.UtcNow });
    }
}