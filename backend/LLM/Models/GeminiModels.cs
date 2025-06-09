using System.Text.Json.Serialization;

namespace Chatbot.LLM.Models;

// Gemini API Request Models
public class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();
    
    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
    
    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; set; }
}

public class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
    
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";
}

public class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;
    
    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; } = 1000;
    
    [JsonPropertyName("topP")]
    public double TopP { get; set; } = 0.8;
    
    [JsonPropertyName("topK")]
    public int TopK { get; set; } = 40;
}

// Gemini API Response Models
public class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate> Candidates { get; set; } = new();
    
    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

public class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent Content { get; set; } = new();
    
    [JsonPropertyName("finishReason")]
    public string FinishReason { get; set; } = string.Empty;
    
    [JsonPropertyName("index")]
    public int Index { get; set; }
}

public class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }
    
    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }
    
    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}

public class GeminiStreamResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate> Candidates { get; set; } = new();
    
    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}