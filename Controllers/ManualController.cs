using Microsoft.AspNetCore.Mvc;
using MetroTechAssistant.Models;
using MetroTechAssistant.Services;

namespace MetroTechAssistant.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ManualController : AiControllerBase
{
    private readonly IManualIngestService _service;

    public ManualController(IManualIngestService service)
    {
        _service = service;
    }

    /// <summary>List the manuals currently stored on the device.</summary>
    [HttpGet]
    public Task<IActionResult> GetAll(CancellationToken ct) =>
        GuardAsync(async () => Ok(await _service.GetManualsAsync()), ct);

    /// <summary>Upload a manual (.pdf, .txt, .md). Uploading the same name again replaces it.</summary>
    [HttpPost("ingest")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(104_857_600)]
    [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
    public Task<IActionResult> Ingest([FromForm] IngestManualRequest request, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest(new { error = "Please choose a file." });

            // Copy to memory so the PDF reader gets a seekable stream.
            await using var buffer = new MemoryStream();
            await request.File.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            var result = await _service.IngestAsync(buffer, request.File.FileName, request.ManualName, ct);
            return Ok(result);
        }, ct);

    /// <summary>Remove a manual from the device.</summary>
    [HttpDelete("{manualName}")]
    public Task<IActionResult> Delete(string manualName, CancellationToken ct) =>
        GuardAsync(async () =>
        {
            await _service.DeleteManualAsync(manualName);
            return NoContent();
        }, ct);
}
