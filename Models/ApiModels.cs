using Microsoft.AspNetCore.Http;

namespace MetroTechAssistant.Models;

public class StatusResponse
{
    public bool OllamaReachable { get; set; }
    public string ChatModel { get; set; } = string.Empty;
    public string EmbedModel { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
}

public class IngestResult
{
    public string ManualName { get; set; } = string.Empty;
    public int Pages { get; set; }
    public int Chunks { get; set; }
}

/// <summary>Multipart form used by POST /api/Manual/ingest.</summary>
public class IngestManualRequest
{
    /// <summary>The manual file (.pdf, .txt or .md).</summary>
    public IFormFile File { get; set; } = default!;

    /// <summary>Optional display name. Defaults to the file name. Re-uploading the same name replaces it.</summary>
    public string? ManualName { get; set; }
}