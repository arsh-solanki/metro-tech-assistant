namespace MetroTechAssistant.Services;

public interface IOllamaClient
{
    string ChatModel { get; }
    string EmbedModel { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    Task<string> ChatAsync(string systemPrompt, string userPrompt, string? imageBase64 = null, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task WarmUpAsync(CancellationToken ct = default);
}
