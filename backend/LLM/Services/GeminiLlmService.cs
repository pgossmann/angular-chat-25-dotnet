using System.Text;
using System.Text.Json;
using Chatbot.LLM.Models;

namespace Chatbot.LLM.Services;

public class GeminiLlmService : ILlmService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiLlmService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    
    public string ProviderName => "Gemini";
    public IEnumerable<string> SupportedModels => new[] { "gemini-2.0-flash", "gemini-2.0-flash-lite", "gemini-2.5-flash-preview-05-20", "gemini-2.5-pro-preview-06-05" };

    public GeminiLlmService(HttpClient httpClient, ILogger<GeminiLlmService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiKey = _configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini API key not configured");
    }

    public async Task<LlmResponse> GetCompletionAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var geminiRequest = BuildGeminiRequest(request);
            var json = JsonSerializer.Serialize(geminiRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{BaseUrl}/{request.Settings.Model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Gemini API error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseJson);

            return MapToLlmResponse(geminiResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            throw;
        }
    }

    public async IAsyncEnumerable<string> GetStreamingCompletionAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var geminiRequest = BuildGeminiRequest(request);
        var json = JsonSerializer.Serialize(geminiRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"{BaseUrl}/{request.Settings.Model}:streamGenerateContent?alt=sse&key={_apiKey}";
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        httpRequest.Headers.Accept.Clear();
        httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending request to Gemini streaming API");
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini streaming API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
            response.Dispose();
            yield break;
        }

        using (response)
        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
        using (var reader = new StreamReader(stream))
        {
            string? line;
            while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // Handle Server-Sent Events format: "data: {json}"
                if (line.StartsWith("data: "))
                {
                    var jsonData = line.Substring(6); // Remove "data: " prefix
                    if (jsonData == "[DONE]") break;
                    
                    var text = TryParseGeminiJsonObject(jsonData);
                    if (!string.IsNullOrEmpty(text))
                    {
                        yield return text;
                    }
                }
            }
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var testRequest = new LlmRequest
            {
                UserMessage = "Hello",
                Settings = new LlmSettings { MaxTokens = 10, Model = "gemini-2.0-flash" }
            };

            await GetCompletionAsync(testRequest, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string? TryParseGeminiJsonObject(string jsonContent)
    {
        try
        {
            var streamResponse = JsonSerializer.Deserialize<GeminiStreamResponse>(jsonContent);
            return streamResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse streaming JSON object: {Json}", jsonContent.Length > 100 ? jsonContent[..100] + "..." : jsonContent);
            return null;
        }
    }

    private string? TryParseGeminiLine(string line)
    {
        try
        {
            var streamResponse = JsonSerializer.Deserialize<GeminiStreamResponse>(line);
            return streamResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse streaming response line: {Line}", line);
            return null;
        }
    }

    private GeminiRequest BuildGeminiRequest(LlmRequest request)
    {
        var geminiRequest = new GeminiRequest
        {
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = request.Settings.Temperature,
                MaxOutputTokens = request.Settings.MaxTokens,
                TopP = 0.8,
                TopK = 40
            }
        };

        // Add system instruction if provided
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            geminiRequest.SystemInstruction = new GeminiContent
            {
                Parts = new List<GeminiPart> { new() { Text = request.SystemPrompt } }
            };
        }

        // Add context if provided
        var contextMessage = BuildContextMessage(request.Context, request.ChatHistory);
        if (!string.IsNullOrEmpty(contextMessage))
        {
            geminiRequest.Contents.Add(new GeminiContent
            {
                Role = "user",
                Parts = new List<GeminiPart> { new() { Text = contextMessage } }
            });
        }

        // Add chat history
        foreach (var message in request.ChatHistory)
        {
            var role = message.Role == "assistant" ? "model" : "user";
            geminiRequest.Contents.Add(new GeminiContent
            {
                Role = role,
                Parts = new List<GeminiPart> { new() { Text = message.Content } }
            });
        }

        // Add current user message
        geminiRequest.Contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = new List<GeminiPart> { new() { Text = request.UserMessage } }
        });

        return geminiRequest;
    }

    private string BuildContextMessage(string context, List<ChatMessage> chatHistory)
    {
        if (string.IsNullOrEmpty(context)) return string.Empty;

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Context Information:");
        contextBuilder.AppendLine(context);
        
        if (chatHistory.Any())
        {
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("Previous conversation context:");
            foreach (var msg in chatHistory.TakeLast(3)) // Limit context to last 3 messages
            {
                contextBuilder.AppendLine($"{msg.Role}: {msg.Content}");
            }
        }

        return contextBuilder.ToString();
    }

    private LlmResponse MapToLlmResponse(GeminiResponse? geminiResponse)
    {
        if (geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text is not string content)
        {
            throw new InvalidOperationException("No valid response from Gemini API");
        }

        return new LlmResponse
        {
            Content = content,
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Usage = geminiResponse.UsageMetadata != null ? new LlmUsage
            {
                PromptTokens = geminiResponse.UsageMetadata.PromptTokenCount,
                CompletionTokens = geminiResponse.UsageMetadata.CandidatesTokenCount,
                TotalTokens = geminiResponse.UsageMetadata.TotalTokenCount
            } : null
        };
    }
}