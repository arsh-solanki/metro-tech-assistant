using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using UglyToad.PdfPig;
using MetroTechAssistant.Helpers;
using MetroTechAssistant.Models;
using MetroTechAssistant.Repositories;

namespace MetroTechAssistant.Services;

/// <summary>Reads a manual (PDF / text), splits it into chunks, embeds each chunk and stores it in SQL Server.</summary>
public class ManualIngestService : IManualIngestService
{
    private readonly IManualChunkRepo _repo;
    private readonly IOllamaClient _ollama;
    private readonly IMemoryCache _cache;
    private readonly int _chunkSize;
    private readonly int _chunkOverlap;
    private readonly int _batchSize;

    public ManualIngestService(IManualChunkRepo repo, IOllamaClient ollama, IMemoryCache cache, IConfiguration config)
    {
        _repo = repo;
        _ollama = ollama;
        _cache = cache;
        _chunkSize = config.GetValue("Rag:ChunkSize", 800);
        _chunkOverlap = config.GetValue("Rag:ChunkOverlap", 120);
        _batchSize = config.GetValue("Rag:EmbedBatchSize", 16);
    }

    public async Task<IngestResult> IngestAsync(Stream fileStream, string fileName, string? manualName, CancellationToken ct = default)
    {
        var name = string.IsNullOrWhiteSpace(manualName) ? Path.GetFileNameWithoutExtension(fileName) : manualName.Trim();
        if (name.Length > 200) name = name[..200];

        var pages = ReadPages(fileStream, fileName);

        var pieces = new List<(int Page, string Text)>();
        foreach (var (pageNo, text) in pages)
            foreach (var chunk in ChunkText(text, _chunkSize, _chunkOverlap))
                pieces.Add((pageNo, chunk));

        if (pieces.Count == 0)
            throw new InvalidOperationException(
                "No readable text was found in the file. Scanned PDFs need OCR first, or upload a .txt version.");

        // Embed everything first, so a failure does not wipe the previously stored manual.
        var vectors = new List<float[]>(pieces.Count);
        for (int i = 0; i < pieces.Count; i += _batchSize)
        {
            var batch = pieces.Skip(i).Take(_batchSize).Select(p => p.Text).ToList();
            vectors.AddRange(await _ollama.EmbedBatchAsync(batch, ct));
        }

        await _repo.DeleteByManualAsync(name);

        for (int i = 0; i < pieces.Count; i++)
        {
            await _repo.InsertChunkAsync(new ManualChunk
            {
                ManualName = name,
                PageNo = pieces[i].Page,
                Content = pieces[i].Text,
                Embedding = vectors[i]
            });
        }

        _cache.Remove(CacheKeys.ManualChunks);

        return new IngestResult { ManualName = name, Pages = pages.Count, Chunks = pieces.Count };
    }

    public Task<List<ManualSummary>> GetManualsAsync() => _repo.GetManualSummariesAsync();

    public async Task DeleteManualAsync(string manualName)
    {
        await _repo.DeleteByManualAsync(manualName);
        _cache.Remove(CacheKeys.ManualChunks);
    }

    // ---------- reading ----------

    private static List<(int PageNo, string Text)> ReadPages(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".pdf")
        {
            var pages = new List<(int, string)>();
            using var document = PdfDocument.Open(stream);
            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    pages.Add((page.Number, text));
            }
            return pages;
        }

        if (extension is ".txt" or ".md")
        {
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            // A form-feed character (\f) separates pages in plain-text manuals.
            return content.Split('\f')
                .Select((text, index) => (PageNo: index + 1, Text: text))
                .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                .ToList();
        }

        throw new ArgumentException("Only .pdf, .txt and .md files are supported.");
    }

    // ---------- chunking ----------

    private static IEnumerable<string> ChunkText(string text, int size, int overlap)
    {
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"(\r?\n){3,}", "\n\n").Trim();
        if (text.Length == 0) yield break;

        if (text.Length <= size)
        {
            yield return text;
            yield break;
        }

        var sentenceEnds = new[] { '\n', '.', '?', '!', '\u0964' }; // \u0964 = Gujarati/Hindi danda
        int start = 0;

        while (start < text.Length)
        {
            int end = Math.Min(start + size, text.Length);

            if (end < text.Length)
            {
                // Prefer to cut at a sentence/line end, then at a space, within the last 40% of the window.
                int minBreak = start + (int)(size * 0.6);
                int count = end - minBreak;

                int cut = text.LastIndexOfAny(sentenceEnds, end - 1, count);
                if (cut >= minBreak)
                {
                    end = cut + 1;
                }
                else
                {
                    cut = text.LastIndexOf(' ', end - 1, count);
                    if (cut >= minBreak) end = cut + 1;
                }
            }

            var chunk = text[start..end].Trim();
            if (chunk.Length > 0) yield return chunk;

            if (end >= text.Length) yield break;

            int next = end - overlap;
            start = next > start ? next : end;
        }
    }
}


//using MetroTechAssistant.Models;

//namespace MetroTechAssistant.Services
//{
//    public class ManualIngestService
//    {
//        public async Task<AskResponse> AskAsync(AskRequest req)
//        {
//            var query = req.Question ?? "";

//            // 1. Photo → text description of the fault
//            if (!string.IsNullOrEmpty(req.ImageBase64))
//            {
//                var desc = await _ollama.ChatAsync(
//                    "You are a metro maintenance assistant. Describe the visible fault, error code or damaged part in one or two sentences.",
//                    "What fault is shown?", req.ImageBase64);
//                query = $"{query} {desc}".Trim();
//            }

//            // 2. Retrieve top chunks
//            var qVec = await _ollama.EmbedAsync(query);
//            var chunks = await _repo.GetAllChunks();            // consider caching in memory
//            var top = chunks.OrderByDescending(c => Cosine(qVec, c.Embedding)).Take(4).ToList();

//            // 3. Generate grounded answer in requested language
//            var lang = req.Language == "gu" ? "Gujarati" : "English";
//            var system = $"You are an offline troubleshooting assistant for metro technicians. " +
//                         $"Answer ONLY from the manual excerpts provided. Reply in {lang} as short numbered steps. " +
//                         $"Start with safety warnings if the excerpts mention any. If the excerpts don't cover the problem, say so.";
//            var context = string.Join("\n\n", top.Select(c => $"[Page {c.PageNo}] {c.Content}"));
//            var answer = await _ollama.ChatAsync(system, $"Manual excerpts:\n{context}\n\nFault: {query}");

//            return new AskResponse
//            {
//                Answer = answer,
//                Sources = top.Select(c => new SourceRef { ManualName = c.ManualName, PageNo = c.PageNo, Snippet = c.Content[..Math.Min(150, c.Content.Length)] }).ToList()
//            };
//        }
//    }
//}
