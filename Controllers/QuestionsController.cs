using Microsoft.AspNetCore.Mvc;
using RagApi.Models;
using RagApi.Services;

namespace RagApi.Controllers;

[ApiController]
[Route("api/questions")]
public class QuestionsController(RagService rag) : ControllerBase
{
    /// <summary>Ask a question about the indexed documents.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AskResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ask([FromBody] AskRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await rag.AskAsync(request, ct));
        }
        catch (EmptyKnowledgeBaseException ex)
        {
            return Conflict(new ProblemDetails { Title = "Knowledge base is empty", Detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid request", Detail = ex.Message });
        }
    }
}