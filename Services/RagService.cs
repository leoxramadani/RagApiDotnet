using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using RagApi.Models;
using RagApi.Options;

namespace RagApi.Services;

public class RagService(
    EmbeddingService embeddings, VectorStoreService store, OpenAIClient openAi,
    IOptions<OpenAiOptions> openAiOpts, IOptions<RagOptions> ragOpts)
{
    public async Task<AskResponse> AskAsync(AskRequest req, CancellationToken ct)
    {
        var stats = await store.GetStatsAsync(ct);
        if (stats.PointsCount == 0) throw new EmptyKnowledgeBaseException();

        var chunks = await RetrieveAsync(req.Question, req.TopK ?? ragOpts.Value.DefaultTopK, ct);
        var answer = await GenerateAsync(req.Question, chunks, req.Model, req.Temperature ?? 0f, ct);

        var sources = chunks.Select((c, i) => new SourceInfo(
            i + 1, c.DocumentId, c.ChunkId, c.Score,
            c.Text.Length > 160 ? c.Text[..160] + "..." : c.Text)).ToList();

        return new AskResponse(answer, RagPrompts.IsFallback(answer), sources);
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string question, int topK, CancellationToken ct)
    {
        var vector = await embeddings.EmbedAsync(question, ct);
        var hits = await store.SearchAsync(vector, topK, ct);
        return hits.Where(h => h.Score >= ragOpts.Value.MinScore).ToList();
    }

    public async Task<string> GenerateAsync(
        string question, IReadOnlyList<RetrievedChunk> chunks, string? model, float temperature, CancellationToken ct)
    {
        // Nothing relevant retrieved: refuse without spending an LLM call
        if (chunks.Count == 0) return RagPrompts.FallbackAnswer;

        var chosen = model ?? openAiOpts.Value.ChatModel;
        if (!openAiOpts.Value.AllowedChatModels.Contains(chosen))
            throw new ArgumentException(
                $"Model '{chosen}' is not allowed. Allowed: {string.Join(", ", openAiOpts.Value.AllowedChatModels)}");

        var messages = new ChatMessage[]
        {
            new SystemChatMessage(RagPrompts.SystemPrompt),
            new UserChatMessage(RagPrompts.BuildUserMessage(RagPrompts.BuildContext(chunks), question))
        };

        var completion = await openAi.GetChatClient(chosen)
            .CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = temperature }, ct);

        return completion.Value.Content[0].Text;
    }
}