using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MetroTechAssistant.Services;

/// <summary>Talks to the locally running Ollama server (no internet needed).</summary>
public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly string _keepAlive;
    private readonly int _numCtx;

    public string ChatModel { get; }
    public string EmbedModel { get; }

    public OllamaClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        ChatModel = config["Ollama:ChatModel"] ?? throw new InvalidOperationException("Ollama:ChatModel is not configured.");
        EmbedModel = config["Ollama:EmbedModel"] ?? throw new InvalidOperationException("Ollama:EmbedModel is not configured.");
        _keepAlive = config["Ollama:KeepAlive"] ?? "30m";
        _numCtx = config.GetValue("Ollama:NumCtx", 4096);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var result = await EmbedBatchAsync(new[] { text }, ct);
        return result[0];
    }

    public async Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return new List<float[]>();

        using var response = await _http.PostAsJsonAsync(
            "/api/embed",
            new { model = EmbedModel, input = texts, keep_alive = _keepAlive },
            ct);
        await EnsureSuccessAsync(response, ct);

        var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: ct);
        if (body?.Embeddings == null || body.Embeddings.Count != texts.Count)
            throw new InvalidOperationException("The embedding model returned an unexpected result.");

        return body.Embeddings;
    }

    public async Task<string> ChatAsync(string systemPrompt, string userPrompt, string? imageBase64 = null, CancellationToken ct = default)
    {
        var userMessage = new Dictionary<string, object>
        {
            ["role"] = "user",
            ["content"] = userPrompt
        };
        if (!string.IsNullOrWhiteSpace(imageBase64))
            userMessage["images"] = new[] { imageBase64 };

        var request = new
        {
            model = ChatModel,
            stream = false,
            keep_alive = _keepAlive,
            options = new { temperature = 0.2, num_ctx = _numCtx },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                userMessage
            }
        };

        using var response = await _http.PostAsJsonAsync("/api/chat", request, ct);
        await EnsureSuccessAsync(response, ct);

        var body = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
        var text = body?.Message?.Content?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("The AI model returned an empty answer.");

        return text;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.GetAsync("/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task WarmUpAsync(CancellationToken ct = default)
    {
        await EmbedAsync("warm up", ct);
        await ChatAsync("Reply with one word.", "ok", null, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"Ollama returned {(int)response.StatusCode}: {detail}");
    }

    private sealed class EmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]>? Embeddings { get; set; }
    }

    private sealed class ChatResponse
    {
        [JsonPropertyName("message")]
        public ChatMessage? Message { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}


//namespace MetroTechAssistant.Services
//{
//    public class OllamaClient
//    {
//        private readonly HttpClient _http; private readonly string _chat, _embed;
//        public OllamaClient(HttpClient http, IConfiguration cfg)
//        {
//            _http = http; _http.BaseAddress = new Uri(cfg["Ollama:BaseUrl"]);
//            _http.Timeout = TimeSpan.FromMinutes(3);      // local models can be slow
//            _chat = cfg["Ollama:ChatModel"]; _embed = cfg["Ollama:EmbedModel"];
//        }

//        public async Task<float[]> EmbedAsync(string text)
//        {
//            var res = await _http.PostAsJsonAsync("/api/embed", new { model = _embed, input = text });
//            var json = await res.Content.ReadFromJsonAsync<EmbedResult>();
//            return json.embeddings[0];
//        }

//        public async Task<string> ChatAsync(string system, string user, string? imageBase64 = null)
//        {
//            var userMsg = new Dictionary<string, object> { ["role"] = "user", ["content"] = user };
//            if (imageBase64 != null) userMsg["images"] = new[] { imageBase64 };
//            var body = new
//            {
//                model = _chat,
//                stream = false,
//                messages = new object[] { new { role = "system", content = system }, userMsg }
//            };
//            var res = await _http.PostAsJsonAsync("/api/chat", body);
//            var json = await res.Content.ReadFromJsonAsync<ChatResult>();
//            return json.message.content;
//        }
//        private record EmbedResult(float[][] embeddings);
//        private record ChatResult(ChatMsg message);
//        private record ChatMsg(string content);
//    }
//}
