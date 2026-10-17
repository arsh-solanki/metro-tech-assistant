using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace MetroTechAssistant.Controllers;

/// <summary>Turns common failures (Ollama down, SQL error, bad input) into clear HTTP responses.</summary>
public abstract class AiControllerBase : ControllerBase
{
    protected async Task<IActionResult> GuardAsync(Func<Task<IActionResult>> action, CancellationToken ct)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            return StatusCode(503, new
            {
                error = "The local AI model (Ollama) is not reachable. Make sure Ollama is running and the models are installed.",
                details = ex.Message
            });
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return StatusCode(504, new { error = "The local AI model took too long to respond. Try a smaller model or a shorter question." });
        }
        catch (SqlException ex)
        {
            return StatusCode(500, new { error = "Database error.", details = ex.Message });
        }
    }
}
