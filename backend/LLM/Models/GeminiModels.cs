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
    public string? Text { get; set; }
    
    [JsonPropertyName("fileData")]
    public GeminiFileData? FileData { get; set; }
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

// Gemini Caching API Models
public class GeminiCacheRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();
    
    [JsonPropertyName("systemInstruction")]
    public GeminiContent? SystemInstruction { get; set; }
    
    [JsonPropertyName("ttl")]
    public string Ttl { get; set; } = "3600s"; // 1 hour default
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
}

public class GeminiCacheResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [JsonPropertyName("usageMetadata")]
    public GeminiCacheUsageMetadata? UsageMetadata { get; set; }
    
    [JsonPropertyName("createTime")]
    public string CreateTime { get; set; } = string.Empty;
    
    [JsonPropertyName("updateTime")]
    public string UpdateTime { get; set; } = string.Empty;
    
    [JsonPropertyName("expireTime")]
    public string ExpireTime { get; set; } = string.Empty;
}

public class GeminiCacheUsageMetadata
{
    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}


public class GeminiFileData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;
    
    [JsonPropertyName("fileUri")]
    public string FileUri { get; set; } = string.Empty;
}

public class GeminiFileUploadRequest
{
    [JsonPropertyName("file")]
    public GeminiFile File { get; set; } = new();
}

public class GeminiFile
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;
}

public class GeminiFileUploadResponse
{
    [JsonPropertyName("file")]
    public GeminiFileInfo File { get; set; } = new();
}

public class GeminiFileInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;
    
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = string.Empty;
    
    [JsonPropertyName("sizeBytes")]
    public string SizeBytes { get; set; } = string.Empty;
    
    [JsonPropertyName("createTime")]
    public string CreateTime { get; set; } = string.Empty;
    
    [JsonPropertyName("updateTime")]
    public string UpdateTime { get; set; } = string.Empty;
    
    [JsonPropertyName("expirationTime")]
    public string ExpirationTime { get; set; } = string.Empty;
    
    [JsonPropertyName("sha256Hash")]
    public string Sha256Hash { get; set; } = string.Empty;
    
    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
    
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}