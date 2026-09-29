using Microsoft.AspNetCore.Mvc;
using RagApi.Models;
using RagApi.Services;

namespace RagApi.Controllers;

[ApiController]
[Route("api/evaluation")]
public class EvaluationController(EvaluationService evaluation) : ControllerBase
{
    /// <summary>Run the benchmark. Paste your eval_dataset.json array as the body.</summary>
    [HttpPost("run")]
    public async Task<ActionResult<EvalReport>> Run(
        [FromBody] List<EvalCase> cases, [FromQuery] int topK = 5, CancellationToken ct = default)
    {
        if (cases.Count == 0) return BadRequest("Provide at least one test case.");
        return await evaluation.RunAsync(cases, Math.Clamp(topK, 1, 10), ct);
    }
}