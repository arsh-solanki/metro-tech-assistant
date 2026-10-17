using MetroTechAssistant.Models;

namespace MetroTechAssistant.Repositories;

public interface IManualChunkRepo
{
    Task<List<ManualChunk>> GetAllChunksAsync();
    Task<List<ManualSummary>> GetManualSummariesAsync();
    Task<int> InsertChunkAsync(ManualChunk chunk);
    Task<int> DeleteByManualAsync(string manualName);
}
