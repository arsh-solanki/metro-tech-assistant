using MetroTechAssistant.Models;

namespace MetroTechAssistant.Services;

public interface IAssistantService
{
    Task<AskResponse> AskAsync(AskRequest request, CancellationToken ct = default);
    Task<StatusResponse> GetStatusAsync(CancellationToken ct = default);
}
