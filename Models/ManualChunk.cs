namespace MetroTechAssistant.Models;

/// <summary>One searchable piece of a technical manual, with its embedding vector.</summary>
public class ManualChunk
{
    public int Id { get; set; }
    public string ManualName { get; set; } = string.Empty;
    public int PageNo { get; set; }
    public string Content { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

public class ManualSummary
{
    public string ManualName { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public int PageCount { get; set; }
}