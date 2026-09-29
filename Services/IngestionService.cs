using RagApi.Models;

namespace RagApi.Services;

public class IngestionService(
    IDocumentTextExtractor extractor, RecursiveTextSplitter splitter,
    EmbeddingService embeddings, VectorStoreService store, ILogger<IngestionService> log)
{
    public async Task<UploadResponse> IngestAsync(
        IFormFile file, int chunkSize, int overlap, CancellationToken ct)
    {
        var documentId = Path.GetFileName(file.FileName);
        var ext = Path.GetExtension(documentId).TrimStart('.').ToLowerInvariant();

        var extracted = await extractor.ExtractAsync(file, ct);
        var chunks = extracted.PreChunked ?? splitter.Split(extracted.Text, chunkSize, overlap);
        if (chunks.Count == 0)
            throw new InvalidOperationException(
                "No text could be extracted (is this a scanned PDF? OCR is not supported).");

        var vectors = await embeddings.EmbedBatchAsync(chunks, ct);

        await store.EnsureCollectionAsync(ct);
        await store.DeleteDocumentAsync(documentId, ct);   // re-uploading replaces instead of duplicating
        await store.UpsertChunksAsync(documentId, ext, chunks, vectors, ct);

        log.LogInformation("Indexed {Doc}: {Count} chunks", documentId, chunks.Count);
        return new UploadResponse(documentId, chunks.Count, extracted.Text.Length);
    }
}