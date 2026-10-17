using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using MetroTechAssistant.Helpers;
using MetroTechAssistant.Models;
using MetroTechAssistant.Repositories;

namespace MetroTechAssistant.Services;

/// <summary>
/// Retrieval-augmented generation: photo -> fault text -> find manual chunks -> grounded answer.
/// </summary>
public class AssistantService : IAssistantService
{
    private static readonly Regex FaultCodeRegex =
        new(@"\b[A-Za-z]{1,4}[-_ ]?\d{1,5}\b", RegexOptions.Compiled);

    private readonly IManualChunkRepo _repo;
    private readonly IOllamaClient _ollama;
    private readonly IMemoryCache _cache;
    private readonly int _topK;
    private readonly double _minScore;
    private readonly int _maxImageLength;

    public AssistantService(IManualChunkRepo repo, IOllamaClient ollama, IMemoryCache cache, IConfiguration config)
    {
        _repo = repo;
        _ollama = ollama;
        _cache = cache;
        _topK = config.GetValue("Rag:TopK", 4);
        _minScore = config.GetValue("Rag:MinScore", 0.25);
        _maxImageLength = config.GetValue("Rag:MaxImageBase64Length", 8_000_000);
    }

    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var language = NormalizeLanguage(request.Language);
        var question = (request.Question ?? string.Empty).Trim();
        var image = CleanBase64(request.ImageBase64);

        if (question.Length == 0 && image == null)
            throw new ArgumentException("Describe the fault or attach a photo.");

        if (image != null && image.Length > _maxImageLength)
            throw new ArgumentException("The photo is too large. Please resize it and try again.");

        // 1. Photo -> short text description of the fault (vision model)
        string? detectedFault = null;
        if (image != null)
        {
            detectedFault = await _ollama.ChatAsync(
                "You are a metro maintenance assistant. Look at the photo and describe the visible fault, " +
                "damaged part or error message in one or two short sentences. Copy any error codes exactly as shown.",
                "What fault is shown in this photo?",
                image,
                ct);
        }

        var searchText = string.Join(" ", new[] { question, detectedFault }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // 2. Retrieve the most relevant manual chunks
        var chunks = await GetChunksAsync();
        if (chunks.Count == 0)
            return Build(sw, language, Messages.NoManual(language), false, detectedFault, new());

        var queryVector = await _ollama.EmbedAsync(searchText, ct);
        var codes = ExtractFaultCodes(searchText);

        var ranked = chunks
            .Where(c => c.Embedding.Length > 0)
            .Select(c => new { Chunk = c, Score = Score(queryVector, c, codes) })
            .OrderByDescending(x => x.Score)
            .Take(_topK)
            .ToList();

        var relevant = ranked.Where(x => x.Score >= _minScore).ToList();
        if (relevant.Count == 0)
            return Build(sw, language, Messages.NotCovered(language), false, detectedFault, new());

        // 3. Generate a grounded answer in the requested language
        var languageName = language == "gu" ? "Gujarati" : "English";
        var systemPrompt =
            "You are an offline troubleshooting assistant for metro maintenance technicians. " +
            "Answer ONLY using the manual excerpts provided. Never invent steps, part names, fault codes or values. " +
            "If the excerpts do not cover the problem, say that clearly. " +
            $"Write the answer in {languageName} as short numbered steps. " +
            "If the excerpts contain safety warnings (power isolation, lock-out, PPE), list them first. " +
            "Keep fault codes, part names and numbers exactly as written in the manual.";

        var context = string.Join("\n\n",
            relevant.Select(x => $"[Page {x.Chunk.PageNo} - {x.Chunk.ManualName}]\n{x.Chunk.Content}"));

        var userPrompt =
            $"Manual excerpts:\n{context}\n\n" +
            $"Technician's problem: {searchText}\n\n" +
            $"Give the troubleshooting steps in {languageName}.";

        var answer = await _ollama.ChatAsync(systemPrompt, userPrompt, null, ct);

        var sources = relevant.Select(x => new SourceRef
        {
            ManualName = x.Chunk.ManualName,
            PageNo = x.Chunk.PageNo,
            Snippet = Truncate(x.Chunk.Content, 200),
            Score = Math.Round(x.Score, 3)
        }).ToList();

        return Build(sw, language, answer, true, detectedFault, sources);
    }

    public async Task<StatusResponse> GetStatusAsync(CancellationToken ct = default)
    {
        var chunks = await _repo.GetAllChunksAsync();
        return new StatusResponse
        {
            OllamaReachable = await _ollama.IsAvailableAsync(ct),
            ChatModel = _ollama.ChatModel,
            EmbedModel = _ollama.EmbedModel,
            ChunkCount = chunks.Count
        };
    }

    // ---------- helpers ----------

    private async Task<List<ManualChunk>> GetChunksAsync()
    {
        if (_cache.TryGetValue(CacheKeys.ManualChunks, out List<ManualChunk>? cached) && cached != null)
            return cached;

        var chunks = await _repo.GetAllChunksAsync();
        _cache.Set(CacheKeys.ManualChunks, chunks);
        return chunks;
    }

    /// <summary>Cosine similarity, plus a small boost when the chunk contains an exact fault code from the query.</summary>
    private static double Score(float[] queryVector, ManualChunk chunk, List<string> codes)
    {
        var score = VectorMath.Cosine(queryVector, chunk.Embedding);

        if (codes.Count > 0)
        {
            var hits = codes.Count(code => chunk.Content.Contains(code, StringComparison.OrdinalIgnoreCase));
            score += Math.Min(0.3, hits * 0.15);
        }
        return score;
    }

    private static List<string> ExtractFaultCodes(string text) =>
        FaultCodeRegex.Matches(text)
            .Select(m => m.Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string NormalizeLanguage(string? language) =>
        string.Equals(language, "gu", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(language, "gujarati", StringComparison.OrdinalIgnoreCase)
            ? "gu"
            : "en";

    /// <summary>Removes an optional "data:image/...;base64," prefix.</summary>
    private static string? CleanBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var comma = value.IndexOf(',');
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
            value = value[(comma + 1)..];
        return value.Trim();
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "...";

    private static AskResponse Build(Stopwatch sw, string language, string answer, bool grounded,
        string? detectedFault, List<SourceRef> sources) =>
        new()
        {
            Answer = answer,
            Language = language,
            Grounded = grounded,
            DetectedFault = detectedFault,
            Sources = sources,
            ElapsedMs = sw.ElapsedMilliseconds
        };

    private static class Messages
    {
        public static string NoManual(string lang) => lang == "gu"
            ? "હજી કોઈ મેન્યુઅલ લોડ કરેલું નથી. કૃપા કરીને પહેલા મેન્યુઅલ અપલોડ કરો."
            : "No manual has been loaded yet. Please upload a manual first.";

        public static string NotCovered(string lang) => lang == "gu"
            ? "આ સમસ્યા માટે મેન્યુઅલમાં સ્પષ્ટ માહિતી મળી નથી. કૃપા કરીને સુપરવાઇઝરનો સંપર્ક કરો."
            : "The manual does not clearly cover this problem. Please contact your supervisor.";
    }
}
