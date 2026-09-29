using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagApi.Models;
using RagApi.Options;

namespace RagApi.Services;

public class VectorStoreService(QdrantClient client, IOptions<QdrantOptions> options)
{
    private readonly QdrantOptions _opt = options.Value;

    public async Task EnsureCollectionAsync(CancellationToken ct)
    {
        if (!await client.CollectionExistsAsync(_opt.CollectionName, ct))
        {
            await client.CreateCollectionAsync(
                _opt.CollectionName,
                new VectorParams
                {
                    Size = (ulong)_opt.VectorSize,
                    Distance = Distance.Cosine
                }, cancellationToken: ct);
        }

        try
        {
            await client.CreatePayloadIndexAsync(
                _opt.CollectionName,
                "document_id",
                PayloadSchemaType.Keyword,
                cancellationToken: ct);
        }
        catch (Grpc.Core.RpcException)
        {
            // Index already exists or creation in progress
        }
    }

    public async Task UpsertChunksAsync(
        string documentId, string sourceType,
        IReadOnlyList<string> chunks, IReadOnlyList<float[]> vectors, CancellationToken ct)
    {
        var points = new List<PointStruct>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var point = new PointStruct { Id = Guid.NewGuid(), Vectors = vectors[i] };
            point.Payload["text"] = chunks[i];
            point.Payload["document_id"] = documentId;
            point.Payload["source_type"] = sourceType;
            point.Payload["chunk_id"] = (long)(i + 1);
            point.Payload["total_chunks"] = (long)chunks.Count;
            points.Add(point);
        }

        foreach (var batch in points.Chunk(100))
            await client.UpsertAsync(_opt.CollectionName, batch.ToList(), cancellationToken: ct);
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken ct)
    {
        if (!await client.CollectionExistsAsync(_opt.CollectionName, ct)) return;
        try
        {
            await client.DeleteAsync(
                _opt.CollectionName, Conditions.MatchKeyword("document_id", documentId), cancellationToken: ct);
        }
        catch (Grpc.Core.RpcException ex) when (ex.Status.Detail.Contains("Index required"))
        {
            await client.CreatePayloadIndexAsync(
                _opt.CollectionName, "document_id", PayloadSchemaType.Keyword, cancellationToken: ct);
            await client.DeleteAsync(
                _opt.CollectionName, Conditions.MatchKeyword("document_id", documentId), cancellationToken: ct);
        }
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(float[] vector, int topK, CancellationToken ct)
    {
        var hits = await client.SearchAsync(
            _opt.CollectionName, vector, limit: (ulong)topK, payloadSelector: true, cancellationToken: ct);

        return hits.Select(h => new RetrievedChunk(
            h.Payload.TryGetValue("text", out var textVal) ? textVal.StringValue : string.Empty,
            h.Payload.TryGetValue("document_id", out var docVal) ? docVal.StringValue : string.Empty,
            h.Payload.TryGetValue("chunk_id", out var chunkVal) ? (int)chunkVal.IntegerValue : 0,
            h.Score)).ToList();
    }
    public async Task<CollectionStats> GetStatsAsync(CancellationToken ct)
    {
        if (!await client.CollectionExistsAsync(_opt.CollectionName, ct))
            return new CollectionStats(false, 0);
        var info = await client.GetCollectionInfoAsync(_opt.CollectionName, ct);
        return new CollectionStats(true, (long)info.PointsCount);
    }

    public async Task<List<IndexedDocument>> GetIndexedDocumentsAsync(CancellationToken ct)
    {
        if (!await client.CollectionExistsAsync(_opt.CollectionName, ct)) return [];

        var docs = new Dictionary<string, (string Type, int Count)>();
        PointId? offset = null;
        do
        {
            var page = await client.ScrollAsync(
                _opt.CollectionName, limit: 256, offset: offset,
                payloadSelector: true, vectorsSelector: false, cancellationToken: ct);

            foreach (var p in page.Result)
            {
                var id = p.Payload.TryGetValue("document_id", out var idVal) ? idVal.StringValue : null;
                if (string.IsNullOrEmpty(id)) continue;

                var type = p.Payload.TryGetValue("source_type", out var typeVal) ? typeVal.StringValue : "unknown";
                docs[id] = docs.TryGetValue(id, out var cur) ? (cur.Type, cur.Count + 1) : (type, 1);
            }
            offset = page.NextPageOffset;
        } while (offset is not null);

        return docs.Select(kv => new IndexedDocument(kv.Key, kv.Value.Type, kv.Value.Count))
                   .OrderBy(d => d.DocumentId).ToList();
    }
}