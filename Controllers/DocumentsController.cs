using Microsoft.AspNetCore.Mvc;
using RagApi.Models;
using RagApi.Services;

namespace RagApi.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController(IngestionService ingestion, VectorStoreService store) : ControllerBase
{
    /// <summary>Upload and index a PDF, TXT or Excel file.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentRequest request, CancellationToken ct)
    {
        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!DocumentTextExtractor.SupportedExtensions.Contains(ext))
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported file type",
                Detail = $"Supported: {string.Join(", ", DocumentTextExtractor.SupportedExtensions)}"
            });

        if (request.ChunkOverlap >= request.ChunkSize)
            return BadRequest(new ProblemDetails { Title = "ChunkOverlap must be smaller than ChunkSize" });

        try
        {
            return Ok(await ingestion.IngestAsync(request.File, request.ChunkSize, request.ChunkOverlap, ct));
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(new ProblemDetails { Title = "Could not process document", Detail = ex.Message });
        }
    }

    /// <summary>List indexed documents with their chunk counts.</summary>
    [HttpGet]
    public async Task<ActionResult<DocumentListResponse>> List(CancellationToken ct)
    {
        var stats = await store.GetStatsAsync(ct);
        var docs = await store.GetIndexedDocumentsAsync(ct);
        return new DocumentListResponse(stats.PointsCount, docs);
    }

    /// <summary>Remove all chunks of a document.</summary>
    [HttpDelete("{documentId}")]
    public async Task<IActionResult> Delete(string documentId, CancellationToken ct)
    {
        await store.DeleteDocumentAsync(documentId, ct);
        return NoContent();
    }
}