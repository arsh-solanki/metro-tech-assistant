using MetroTechAssistant.Repositories;
using MetroTechAssistant.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Framework services ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
              ?? new[] { "http://localhost:5173" };
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

// ---------- Local AI (Ollama) client ----------
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(cfg["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(cfg.GetValue("Ollama:TimeoutSeconds", 300));
});

// ---------- Dependency injection (same pattern as RepositoryPatternDemo) ----------
builder.Services.AddScoped<IManualChunkRepo, ManualChunkRepository>();
builder.Services.AddScoped<IAssistantService, AssistantService>();
builder.Services.AddScoped<IManualIngestService, ManualIngestService>();

var app = builder.Build();

// Load the models into memory right after startup so the first real question is not slow.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var ollama = scope.ServiceProvider.GetRequiredService<IOllamaClient>();
            await ollama.WarmUpAsync();
            app.Logger.LogInformation("Ollama models warmed up.");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning("Ollama warm-up skipped: {Message}", ex.Message);
        }
    });
});

// No HTTPS redirection on purpose: the demo runs on localhost, offline.
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

// Serves the React build copied into wwwroot (so no Node is needed at demo time).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();


/*
 import { useState } from "react";
import { askAssistant } from "../api/assistantApi";

export function useAssistant() {
  const [messages, setMessages] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const ask = async ({ question, language, imageBase64 }) => {
    setMessages(m => [...m, { role: "user", text: question, image: imageBase64 }]);
    setLoading(true); setError(null);
    try {
      const data = await askAssistant({ question, language, imageBase64 });
      setMessages(m => [...m, { role: "assistant", text: data.answer, sources: data.sources }]);
    } catch {
      setError("Assistant not reachable. Is the local server running?");
    } finally { setLoading(false); }
  };
  return { messages, loading, error, ask };
}
 */