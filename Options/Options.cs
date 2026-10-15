using System.ComponentModel.DataAnnotations;


namespace RagApi.Options;


public class OpenAiOptions
{
    public const string Section = "OpenAI";
    public string ApiKey { get; set; } = "ollama";
    public string? Endpoint { get; set; } = "http://localhost:11434/v1";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.2";
    public string JudgeModel { get; set; } = "llama3.2";
    public string[] AllowedChatModels { get; set; } = { "llama3.2", "llama3.1", "gpt-4o-mini", "gpt-4o" };
}

public class QdrantOptions
{
    public const string Section = "Qdrant";
    [Required] public string Host { get; set; } = "";
    public int Port { get; set; } = 6334;
    public bool UseHttps { get; set; } = true;
    public string? ApiKey { get; set; } = "";
    [Required] public string CollectionName { get; set; } = "rag_local";
    public int VectorSize { get; set; } = 768;
}

public class RagOptions
{
    public const string Section = "Rag";
    public int DefaultTopK { get; set; } = 4;
    /// <summary>Chunks scoring below this are discarded. 0 = disabled. Tune per embedding model.</summary>
    public float MinScore { get; set; } = 0f;
}