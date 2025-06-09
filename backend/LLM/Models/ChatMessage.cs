namespace Chatbot.LLM.Models;

public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "user"; // "user", "assistant", "system"
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class LlmRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<ChatMessage> ChatHistory { get; set; } = new();
    public string UserMessage { get; set; } = string.Empty;
    public LlmSettings Settings { get; set; } = new();
}

public class LlmResponse
{
    public string Content { get; set; } = string.Empty;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LlmUsage? Usage { get; set; }
}

public class LlmSettings
{
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 1000;
    public string Model { get; set; } = "gemini-2.5-flash-preview-05-20";
}

public class LlmUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}