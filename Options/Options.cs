using System.ComponentModel.DataAnnotations;


namespace RagApi.Options;


public class OpenAiOptions
{
    public const string Section = "OpenAI";
    [Required] public string ApiKey { get; set; } = "";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string JudgeModel { get; set; } = "gpt-4o-mini";
    public string[] AllowedChatModels { get; set; } = { "gpt-4o-mini", "gpt-4o" };
}

public class QdrantOptions
{
    public const string Section = "Qdrant";
    [Required] public string Host { get; set; } = "";
    public int Port { get; set; } = 6334;
    public bool UseHttps { get; set; } = true;
    [Required] public string ApiKey { get; set; } = "";
    [Required] public string CollectionName { get; set; } = "rag_dotnet";
    public int VectorSize { get; set; } = 1536;
}

public class RagOptions
{
    public const string Section = "Rag";
    public int DefaultTopK { get; set; } = 4;
    /// <summary>Chunks scoring below this are discarded. 0 = disabled. Tune per embedding model.</summary>
    public float MinScore { get; set; } = 0f;
}