using System.Net.Http.Headers;
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
    private const string CacheBaseUrl = "https://generativelanguage.googleapis.com/v1beta/cachedContents";
    private const string FileBaseUrl = "https://generativelanguage.googleapis.com/upload/v1beta/files";

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

    public async Task<string> CacheTextContextAsync(string context, string systemPrompt, string model, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheRequest = new GeminiCacheRequest
            {
                Model = $"models/{model}",
                DisplayName = $"TextContext_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Ttl = "3600s" // 1 hour
            };

            // Add system instruction if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                cacheRequest.SystemInstruction = new GeminiContent
                {
                    Parts = new List<GeminiPart> { new() { Text = systemPrompt } }
                };
            }

            // Add context content
            cacheRequest.Contents.Add(new GeminiContent
            {
                Role = "user",
                Parts = new List<GeminiPart> { new() { Text = context } }
            });

            var json = JsonSerializer.Serialize(cacheRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{CacheBaseUrl}?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini cache creation failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Gemini cache creation failed: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var cacheResponse = JsonSerializer.Deserialize<GeminiCacheResponse>(responseJson);

            if (string.IsNullOrEmpty(cacheResponse?.Name))
            {
                throw new InvalidOperationException("Invalid cache response from Gemini API");
            }

            _logger.LogInformation("Created text cache: {CacheId}", cacheResponse.Name);
            return cacheResponse.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating text cache in Gemini");
            throw;
        }
    }

    public async Task<string> CacheFileContextAsync(byte[] fileContent, string fileName, string mimeType, string systemPrompt, string model, CancellationToken cancellationToken = default)
    {
        try
        {
            // First upload the file to Gemini
            var fileUri = await UploadFileToGeminiAsync(fileContent, fileName, mimeType, cancellationToken);

            // Wait for file processing
            await WaitForFileProcessingAsync(fileUri, cancellationToken);

            var cacheRequest = new GeminiCacheRequest
            {
                Model = $"models/{model}",
                DisplayName = $"FileContext_{fileName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                Ttl = "3600s" // 1 hour
            };

            // Add system instruction if provided
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                cacheRequest.SystemInstruction = new GeminiContent
                {
                    Parts = new List<GeminiPart> { new() { Text = systemPrompt } }
                };
            }

            // Add file content
            cacheRequest.Contents.Add(new GeminiContent
            {
                Role = "user",
                Parts = new List<GeminiPart>
                {
                    new GeminiPart
                    {
                        FileData = new GeminiFileData
                        {
                            MimeType = mimeType,
                            FileUri = fileUri
                        }
                    }
                }
            });

            var json = JsonSerializer.Serialize(cacheRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{CacheBaseUrl}?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Gemini file cache creation failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Gemini file cache creation failed: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var cacheResponse = JsonSerializer.Deserialize<GeminiCacheResponse>(responseJson);

            if (string.IsNullOrEmpty(cacheResponse?.Name))
            {
                throw new InvalidOperationException("Invalid cache response from Gemini API");
            }

            _logger.LogInformation("Created file cache: {CacheId} for file: {FileName}", cacheResponse.Name, fileName);
            return cacheResponse.Name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating file cache in Gemini for file: {FileName}", fileName);
            throw;
        }
    }

    public async IAsyncEnumerable<string> GetStreamingCompletionWithCacheAsync(string cacheId, string userMessage, List<ChatMessage> chatHistory, LlmSettings settings, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var geminiRequest = new GeminiRequest
        {
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = settings.Temperature,
                MaxOutputTokens = settings.MaxTokens,
                TopP = 0.8,
                TopK = 40
            }
        };

        // Add chat history
        foreach (var message in chatHistory)
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
            Parts = new List<GeminiPart> { new() { Text = userMessage } }
        });

        var json = JsonSerializer.Serialize(geminiRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Use the cached model endpoint
        var url = $"{BaseUrl}/{cacheId}:streamGenerateContent?alt=sse&key={_apiKey}";

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
            _logger.LogError(ex, "Error sending request to Gemini cached streaming API");
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini cached streaming API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
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

    public async Task<bool> ValidateCacheAsync(string cacheId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{CacheBaseUrl}/{cacheId}?key={_apiKey}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cache validation failed for {CacheId}: {StatusCode}", cacheId, response.StatusCode);
                return false;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var cacheResponse = JsonSerializer.Deserialize<GeminiCacheResponse>(responseJson);

            if (cacheResponse == null)
            {
                return false;
            }

            // Check if cache is expired
            if (!string.IsNullOrEmpty(cacheResponse.ExpireTime))
            {
                if (DateTime.TryParse(cacheResponse.ExpireTime, out var expiry))
                {
                    if (expiry <= DateTime.UtcNow)
                    {
                        _logger.LogInformation("Cache {CacheId} has expired", cacheId);
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating cache {CacheId}", cacheId);
            return false;
        }
    }

    public async Task<bool> DeleteCacheAsync(string cacheId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{CacheBaseUrl}/{cacheId}?key={_apiKey}";
            var response = await _httpClient.DeleteAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully deleted cache: {CacheId}", cacheId);
                return true;
            }

            _logger.LogWarning("Failed to delete cache {CacheId}: {StatusCode}", cacheId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cache {CacheId}", cacheId);
            return false;
        }
    }

    private async Task<string> UploadFileToGeminiAsync(byte[] fileContent, string fileName, string mimeType, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Use correct uploadType=multipart in URL (per Gemini docs) :contentReference[oaicite:1]{index=1}
            var url = $"{FileBaseUrl}?uploadType=multipart&key={_apiKey}";

            using var multipart = new MultipartFormDataContent();

            // 2. Metadata part (include displayName and mimeType in JSON)
            var metadata = new { file = new { displayName = fileName, mimeType = mimeType } };
            var metaJson = JsonSerializer.Serialize(metadata);
            var metaPart = new StringContent(metaJson, Encoding.UTF8, "application/json");
            metaPart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"metadata\"" };
            multipart.Add(metaPart);

            // 3. File part (actual PDF bytes)
            var filePart = new ByteArrayContent(fileContent);
            filePart.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            filePart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
            {
                Name = "\"file\"",
                FileName = $"\"{fileName}\""
            };
            multipart.Add(filePart);

            _logger.LogDebug("Uploading file {FileName} ({Size} bytes, {MimeType}) to Gemini", fileName, fileContent.Length, mimeType);

            var response = await _httpClient.PostAsync(url, multipart, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Gemini file upload response: {Response}", responseJson);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Gemini file upload failed: {StatusCode} - {Content}", response.StatusCode, responseJson);
                throw new HttpRequestException($"Gemini file upload failed: {response.StatusCode} - {responseJson}");
            }

            // 4. Parse response
            var uploadResponse = JsonSerializer.Deserialize<GeminiFileUploadResponse>(responseJson);
            if (string.IsNullOrEmpty(uploadResponse?.File?.Uri))
            {
                _logger.LogError("Invalid file upload response - missing URI: {Response}", responseJson);
                throw new InvalidOperationException($"Invalid file upload response from Gemini API: {responseJson}");
            }

            _logger.LogInformation("File uploaded successfully: {FileName} -> {FileUri}", fileName, uploadResponse.File.Uri);
            return uploadResponse.File.Uri;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to Gemini", fileName);
            throw;
        }
    }

    private async Task WaitForFileProcessingAsync(string fileUri, CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;
        const int delayMs = 1000;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                var url = $"{fileUri}?key={_apiKey}";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    var fileInfo = JsonSerializer.Deserialize<GeminiFileInfo>(responseJson);

                    if (fileInfo?.State == "ACTIVE")
                    {
                        _logger.LogInformation("File processing completed for: {FileUri}", fileUri);
                        return;
                    }
                }

                await Task.Delay(delayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking file processing status for: {FileUri}, attempt {Attempt}", fileUri, attempt + 1);
            }
        }

        throw new TimeoutException($"File processing timeout for: {fileUri}");
    }
}