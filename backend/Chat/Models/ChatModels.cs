using Chatbot.LLM.Models;

namespace Chatbot.Chat.Models;

public class ChatRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<ChatMessage> History { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public ChatSettings Settings { get; set; } = new();
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ChatUsage? Usage { get; set; }
}

public class ChatSettings
{
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1000;
    public string Model { get; set; } = "gemini-2.5-flash-preview-05-20";
    public string Provider { get; set; } = "Gemini";
}

public class ChatUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class StreamingChatResponse
{
    public string Content { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public string MessageId { get; set; } = string.Empty;
}

public class InitializeChatRequest
{
    public string Context { get; set; } = string.Empty;
    public IFormFile? File { get; set; }
    public string SystemPrompt { get; set; } = string.Empty;
    public string FirstMessage { get; set; } = string.Empty;
    public ChatSettings Settings { get; set; } = new();
}

public class ChatMessageRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string UserMessage { get; set; } = string.Empty;
    public ChatSettings? Settings { get; set; }
}

public class InitializeChatResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ChatUsage? Usage { get; set; }
}

public class ConversationContext
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CacheId { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public List<ChatMessage> ChatHistory { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiryAt { get; set; }
    public ChatSettings Settings { get; set; } = new();
    public CachedDocument? Document { get; set; }
    public string OriginalContext { get; set; } = string.Empty;
}

public class CachedDocument
{
    public string CacheId { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    public byte[] OriginalContent { get; set; } = Array.Empty<byte>();
}

public class ConversationListResponse
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiryAt { get; set; }
    public int MessageCount { get; set; }
    public string? DocumentName { get; set; }
    public bool IsActive { get; set; }
}

public class ConversationStatusResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CacheValid { get; set; }
    public DateTime? CacheExpiryAt { get; set; }
    public int MessageCount { get; set; }
    public string? DocumentName { get; set; }
}