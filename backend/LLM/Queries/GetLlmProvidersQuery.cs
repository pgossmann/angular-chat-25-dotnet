using MediatR;
using Chatbot.LLM.Services;

namespace Chatbot.LLM.Queries;

public class GetLlmProvidersQuery : IRequest<LlmProvidersResponse>
{
}

public class LlmProvidersResponse
{
    public List<LlmProviderInfo> Providers { get; set; } = new();
}

public class LlmProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public List<string> SupportedModels { get; set; } = new();
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class GetLlmProvidersQueryHandler : IRequestHandler<GetLlmProvidersQuery, LlmProvidersResponse>
{
    private readonly ILlmService _llmService;
    private readonly ILogger<GetLlmProvidersQueryHandler> _logger;

    public GetLlmProvidersQueryHandler(ILlmService llmService, ILogger<GetLlmProvidersQueryHandler> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<LlmProvidersResponse> Handle(GetLlmProvidersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting LLM providers information");

        var response = new LlmProvidersResponse();

        try
        {
            var isAvailable = await _llmService.IsAvailableAsync(cancellationToken);
            
            response.Providers.Add(new LlmProviderInfo
            {
                Name = _llmService.ProviderName,
                SupportedModels = _llmService.SupportedModels.ToList(),
                IsAvailable = isAvailable,
                Status = isAvailable ? "Available" : "Unavailable"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking LLM provider availability");
            
            response.Providers.Add(new LlmProviderInfo
            {
                Name = _llmService.ProviderName,
                SupportedModels = _llmService.SupportedModels.ToList(),
                IsAvailable = false,
                Status = $"Error: {ex.Message}"
            });
        }

        return response;
    }
}