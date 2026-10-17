namespace MetroTechAssistant.Models;

public class AskResponse
{
    public string Answer { get; set; } = string.Empty;
    public string Language { get; set; } = "en";

    /// <summary>False when the manual did not contain anything relevant (no AI-invented steps).</summary>
    public bool Grounded { get; set; }

    /// <summary>What the vision model saw in the photo (only when a photo was sent).</summary>
    public string? DetectedFault { get; set; }

    public List<SourceRef> Sources { get; set; } = new();
    public long ElapsedMs { get; set; }
}

public class SourceRef
{
    public string ManualName { get; set; } = string.Empty;
    public int PageNo { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public double Score { get; set; }
}