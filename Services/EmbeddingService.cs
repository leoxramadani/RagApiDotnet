using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Embeddings;
using RagApi.Options;

namespace RagApi.Services;

public class EmbeddingService(OpenAIClient openAi, IOptions<OpenAiOptions> options)
{
    private readonly EmbeddingClient _client = openAi.GetEmbeddingClient(options.Value.EmbeddingModel);

    public async Task<float[]> EmbedAsync(string text, CancellationToken ctx = default)
    {
        var result = await _client.GenerateEmbeddingAsync(text, cancellationToken: ctx);
        return result.Value.ToFloats().ToArray();
    }    

    public async Task<List<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ctx = default)
    {
        var all = new List<float[]>(texts.Count);
        foreach(var batch in texts.Chunk(100))
        {
            var result = await _client.GenerateEmbeddingsAsync(batch, cancellationToken: ctx);
            all.AddRange(result.Value.Select(x => x.ToFloats().ToArray()));
        }
        return all;
    }
}