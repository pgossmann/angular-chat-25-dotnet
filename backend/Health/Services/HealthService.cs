using Chatbot.Health.Queries;
using Chatbot.LLM.Services;
using System.Reflection;

namespace Chatbot.Health.Services;

public interface IHealthService
{
    Task<HealthStatusResponse> GetHealthStatusAsync(bool includeDependencies = true, CancellationToken cancellationToken = default);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}

public class HealthService : IHealthService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<HealthService> _logger;
    private readonly DateTime _startTime;

    public HealthService(ILlmService llmService, ILogger<HealthService> logger)
    {
        _llmService = llmService;
        _logger = logger;
        _startTime = DateTime.UtcNow;
    }

    public async Task<HealthStatusResponse> GetHealthStatusAsync(bool includeDependencies = true, CancellationToken cancellationToken = default)
    {
        var response = new HealthStatusResponse
        {
            Timestamp = DateTime.UtcNow,
            Uptime = DateTime.UtcNow - _startTime,
            Version = GetVersion()
        };

        if (includeDependencies)
        {
            response.Dependencies = await CheckDependenciesAsync(cancellationToken);
            
            // Determine overall status based on dependencies
            var hasUnhealthyDependencies = response.Dependencies.Values
                .OfType<Dictionary<string, object>>()
                .Any(dep => dep.ContainsKey("status") && dep["status"].ToString() != "Healthy");

            response.Status = hasUnhealthyDependencies ? "Degraded" : "Healthy";
        }

        return response;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dependencies = await CheckDependenciesAsync(cancellationToken);
            return dependencies.Values
                .OfType<Dictionary<string, object>>()
                .All(dep => dep.ContainsKey("status") && dep["status"].ToString() == "Healthy");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking health status");
            return false;
        }
    }

    private async Task<Dictionary<string, object>> CheckDependenciesAsync(CancellationToken cancellationToken)
    {
        var dependencies = new Dictionary<string, object>();

        // Check LLM Service
        var llmStatus = await CheckLlmServiceAsync(cancellationToken);
        dependencies["llmService"] = llmStatus;

        // Add more dependency checks here as needed
        dependencies["database"] = new Dictionary<string, object>
        {
            ["status"] = "Not Configured",
            ["message"] = "No database configured for this application"
        };

        return dependencies;
    }

    private async Task<Dictionary<string, object>> CheckLlmServiceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var isAvailable = await _llmService.IsAvailableAsync(cancellationToken);
            stopwatch.Stop();

            return new Dictionary<string, object>
            {
                ["status"] = isAvailable ? "Healthy" : "Unhealthy",
                ["provider"] = _llmService.ProviderName,
                ["supportedModels"] = _llmService.SupportedModels.ToList(),
                ["responseTime"] = $"{stopwatch.ElapsedMilliseconds}ms",
                ["message"] = isAvailable ? "LLM service is responding" : "LLM service is not available"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking LLM service health");
            return new Dictionary<string, object>
            {
                ["status"] = "Unhealthy",
                ["provider"] = _llmService.ProviderName,
                ["error"] = ex.Message,
                ["message"] = "LLM service check failed"
            };
        }
    }

    private string GetVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}