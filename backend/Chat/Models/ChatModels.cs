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
    public string Model { get; set; } = "gemini-1.5-flash";
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