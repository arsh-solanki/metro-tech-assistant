using MetroTechAssistant.Models;

namespace MetroTechAssistant.Services;

public interface IManualIngestService
{
    Task<IngestResult> IngestAsync(Stream fileStream, string fileName, string? manualName, CancellationToken ct = default);
    Task<List<ManualSummary>> GetManualsAsync();
    Task DeleteManualAsync(string manualName);
}
