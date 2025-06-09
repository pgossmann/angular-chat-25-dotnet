using Chatbot.Infrastructure;
using Chatbot.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Chatbot API", 
        Version = "v1",
        Description = "A feature-based ASP.NET Core Web API for chatbot functionality with multiple LLM providers support"
    });
});

// Add custom services
builder.Services.AddChatbotServices(builder.Configuration);
builder.Services.AddChatbotConfiguration(builder.Configuration);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Chatbot API v1");
        c.RoutePrefix = "swagger";
    });
}

// Add custom middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

// Configure CORS - must be before UseAuthorization
app.UseCors("AllowFrontend");

app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Add health check endpoints
app.MapGet("/", () => new { 
    message = "Chatbot API is running", 
    timestamp = DateTime.UtcNow,
    version = "1.0.0",
    environment = app.Environment.EnvironmentName
});

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Chatbot API starting up...");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Swagger UI available at: /swagger");

try
{
    // Validate configuration on startup
    var configuration = app.Services.GetRequiredService<IConfiguration>();
    var geminiApiKey = configuration["Gemini:ApiKey"];
    
    if (string.IsNullOrEmpty(geminiApiKey))
    {
        logger.LogWarning("Gemini API key is not configured. Some features may not work.");
    }
    else
    {
        logger.LogInformation("Gemini API key is configured");
    }

    logger.LogInformation("Chatbot API started successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Error during startup validation");
}

app.Run();