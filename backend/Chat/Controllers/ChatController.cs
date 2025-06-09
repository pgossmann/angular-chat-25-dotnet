using Microsoft.AspNetCore.Mvc;
using MediatR;
using Chatbot.Chat.Commands;
using Chatbot.Chat.Models;
using System.Text.Json;
using System.Text;

namespace Chatbot.Chat.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IMediator mediator, ILogger<ChatController> logger)
    {
        _mediator = mediator;
        _logger = logger;
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

    [HttpGet("hello")]
    public IActionResult HelloWorld()
    {
        return Ok(new { message = "Hello World from ASP.NET Core Chatbot!", timestamp = DateTime.UtcNow });
    }
}