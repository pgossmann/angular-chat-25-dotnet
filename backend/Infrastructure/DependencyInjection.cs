using Chatbot.LLM.Services;
using Chatbot.Chat.Services;
using Chatbot.Health.Services;

namespace Chatbot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddChatbotServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Add MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        // Add HttpClient for LLM services
        services.AddHttpClient<GeminiLlmService>();

        // Register LLM Services
        services.AddScoped<ILlmService, GeminiLlmService>();

        // Register Chat Services
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IConversationService, ConversationService>();

        // Register Health Services
        services.AddScoped<IHealthService, HealthService>();

        // Add CORS
        services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                policy.WithOrigins(
                        "http://localhost:4200",
                        "https://localhost:4200",
                        "http://localhost:3000",
                        "https://localhost:3000"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });

            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static IServiceCollection AddChatbotConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Validate required configuration
        ValidateConfiguration(configuration);

        return services;
    }

    private static void ValidateConfiguration(IConfiguration configuration)
    {
        var geminiApiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrEmpty(geminiApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured. Please set the 'Gemini:ApiKey' configuration value.");
        }
    }
}