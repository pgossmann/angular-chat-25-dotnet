using MediatR;

namespace Chatbot.Health.Queries;

public class GetHealthStatusQuery : IRequest<HealthStatusResponse>
{
    public bool IncludeDependencies { get; set; } = true;
}

public class HealthStatusResponse
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Dependencies { get; set; } = new();
    public TimeSpan Uptime { get; set; }
    public string Version { get; set; } = "1.0.0";
}

public class GetHealthStatusQueryHandler : IRequestHandler<GetHealthStatusQuery, HealthStatusResponse>
{
    private readonly Health.Services.IHealthService _healthService;
    private readonly ILogger<GetHealthStatusQueryHandler> _logger;

    public GetHealthStatusQueryHandler(Health.Services.IHealthService healthService, ILogger<GetHealthStatusQueryHandler> logger)
    {
        _healthService = healthService;
        _logger = logger;
    }

    public async Task<HealthStatusResponse> Handle(GetHealthStatusQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing health status request");

        return await _healthService.GetHealthStatusAsync(request.IncludeDependencies, cancellationToken);
    }
}