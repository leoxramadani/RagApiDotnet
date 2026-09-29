using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RagApi.Models;



//Internal
public record RetrievedChunk(string Text, string DocumentId, int ChunkId, float Score);
public record CollectionStats(bool Exists, long PointsCount);
public record ExtractedDocument(string Text, IReadOnlyList<string>? PreChunked);


//Documents
public class UploadDocumentRequest
{
    [Required] public IFormFile File { get; set; } = default!;
    [Range(200, 2000)] public int ChunkSize { get; set; } = 800;
    [Range(0, 400)] public int ChunkOverlap { get; set; } = 150;
}
public record UploadResponse(string DocumentId, int ChunksIndexed, int CharactersExtracted);
public record IndexedDocument(string DocumentId, string SourceType, int ChunkCount);
public record DocumentListResponse(long TotalChunks, IReadOnlyList<IndexedDocument> Documents);


//Questions
public class AskRequest
{
    [Required, StringLength(2000, MinimumLength = 3)]
    public string Question { get; set; } = "";
    [Range(1, 8)] public int? TopK { get; set; }
    [Range(0.0, 0.8)] public float? Temperature { get; set; }
    public string? Model { get; set; }
}
public record SourceInfo(int Index, string DocumentId, int ChunkId, float Score, string Snippet);
public record AskResponse(string Answer, bool IsFallback, IReadOnlyList<SourceInfo> Sources);

//Evaluation
public class EvalCase
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("question")] public string Question { get; set; } = "";
    [JsonPropertyName("expected_document")] public string? ExpectedDocument { get; set; }
    [JsonPropertyName("expected_keywords")] public List<string>? ExpectedKeywords { get; set; }
    [JsonPropertyName("ground_truth_answer")] public string? GroundTruthAnswer { get; set; }
    [JsonPropertyName("is_guardrail_test")] public bool IsGuardrailTest { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
}

public record EvalCaseResult(
    string Id, string Question, bool IsGuardrail, int? HitRank, bool HitAt3, bool HitAt5,
    double ReciprocalRank, double Faithfulness, string JudgeReason, string GeneratedAnswer);

public record EvalReport(
    int TotalCases, double HitRateAt3, double HitRateAt5, double Mrr,
    double GuardrailAccuracy, double AvgFaithfulness, IReadOnlyList<EvalCaseResult> Results);

public class EmptyKnowledgeBaseException : Exception
{
    public EmptyKnowledgeBaseException()
        : base("The knowledge base is empty. Upload documents first.")
    {
    }
}