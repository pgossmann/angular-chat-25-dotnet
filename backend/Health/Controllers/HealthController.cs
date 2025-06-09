using Microsoft.AspNetCore.Mvc;
using MediatR;
using Chatbot.Health.Queries;

namespace Chatbot.Health.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IMediator mediator, ILogger<HealthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<HealthStatusResponse>> GetHealthStatus(
        [FromQuery] bool includeDependencies = true, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetHealthStatusQuery { IncludeDependencies = includeDependencies };
            var response = await _mediator.Send(query, cancellationToken);

            var statusCode = response.Status switch
            {
                "Healthy" => 200,
                "Degraded" => 200, // Still return 200 for degraded but functional
                _ => 503
            };

            return StatusCode(statusCode, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return StatusCode(503, new HealthStatusResponse
            {
                Status = "Unhealthy",
                Timestamp = DateTime.UtcNow,
                Dependencies = new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                }
            });
        }
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReadiness(CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new GetHealthStatusQuery { IncludeDependencies = true };
            var response = await _mediator.Send(query, cancellationToken);

            return response.Status == "Healthy" ? Ok(new { status = "Ready" }) : StatusCode(503, new { status = "Not Ready" });
        }
        catch
        {
            return StatusCode(503, new { status = "Not Ready" });
        }
    }

    [HttpGet("live")]
    public IActionResult GetLiveness()
    {
        // Simple liveness check - if the app is running, it's alive
        return Ok(new { status = "Alive", timestamp = DateTime.UtcNow });
    }
}