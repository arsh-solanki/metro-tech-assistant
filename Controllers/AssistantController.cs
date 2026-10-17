using Microsoft.AspNetCore.Mvc;
using MetroTechAssistant.Models;
using MetroTechAssistant.Services;

namespace MetroTechAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssistantController : AiControllerBase
{
    private readonly IAssistantService _service;

    public AssistantController(IAssistantService service)
    {
        _service = service;
    }

    /// <summary>Technician describes and/or photographs a fault and gets troubleshooting steps from the manual.</summary>
    [HttpPost("ask")]
    [RequestSizeLimit(20_000_000)]
    public Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken ct) =>
        GuardAsync(async () => Ok(await _service.AskAsync(request, ct)), ct);

    /// <summary>Quick check that the database and the local AI server are working.</summary>
    [HttpGet("health")]
    public Task<IActionResult> Health(CancellationToken ct) =>
        GuardAsync(async () => Ok(await _service.GetStatusAsync(ct)), ct);
}