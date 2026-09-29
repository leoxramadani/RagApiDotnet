using System.Text.Json;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using RagApi.Models;
using RagApi.Options;

namespace RagApi.Services;

public class EvaluationService(
    RagService rag, OpenAIClient openAi, IOptions<OpenAiOptions> openAiOpts)
{
    private const string JudgePrompt = """
        You are an impartial evaluation judge assessing the faithfulness and groundedness of a RAG-generated answer.

        [RETRIEVED CONTEXT]:
        {context}

        [QUESTION]:
        {question}

        [GENERATED ANSWER]:
        {answer}

        Task:
        Determine if every factual claim in the [GENERATED ANSWER] is directly supported by the [RETRIEVED CONTEXT].
        If the answer says "Nuk kam informacion për këtë në dokumentet e ngarkuara." and context indeed lacks answer, mark it as faithful (1.0).
        If the answer introduces claims, facts, or assumptions not found anywhere in the context, mark it unfaithful (0.0).

        Return your response strictly in the following JSON format:
        {"score": 1.0, "reason": "short explanation"} or {"score": 0.0, "reason": "short explanation"}
        """;

    public async Task<EvalReport> RunAsync(IReadOnlyList<EvalCase> cases, int topK, CancellationToken ct)
    {
        var results = new List<EvalCaseResult>();

        foreach (var (test, idx) in cases.Select((t, i) => (t, i)))
        {
            var chunks = await rag.RetrieveAsync(test.Question, topK, ct);

            // Hit rank: match on expected document if given, otherwise fall back to keywords
            int? hitRank = null;
            if (!test.IsGuardrailTest)
            {
                for (var r = 0; r < chunks.Count; r++)
                {
                    var c = chunks[r];
                    var hasDoc = !string.IsNullOrWhiteSpace(test.ExpectedDocument);
                    var match = hasDoc
                        ? c.DocumentId.Contains(test.ExpectedDocument!, StringComparison.OrdinalIgnoreCase)
                        : (test.ExpectedKeywords?.Any(k => c.Text.Contains(k, StringComparison.OrdinalIgnoreCase)) ?? false);
                    if (match) { hitRank = r + 1; break; }
                }
            }

            var answer = await rag.GenerateAsync(test.Question, chunks, null, 0f, ct);

            double faith; string reason;
            if (test.IsGuardrailTest)
            {
                var refused = RagPrompts.IsFallback(answer);
                faith = refused ? 1.0 : 0.0;
                reason = refused
                    ? "Guardrail worked: refused an out-of-context question."
                    : "Guardrail failed: answered an out-of-context question.";
            }
            else
            {
                (faith, reason) = await JudgeAsync(RagPrompts.BuildContext(chunks), test.Question, answer, ct);
            }

            results.Add(new EvalCaseResult(
                test.Id ?? $"case-{idx + 1}", test.Question, test.IsGuardrailTest, hitRank,
                HitAt3: hitRank is <= 3, HitAt5: hitRank is <= 5,
                ReciprocalRank: hitRank is null ? 0 : 1.0 / hitRank.Value,
                faith, reason, answer));
        }

        var retrieval = results.Where(r => !r.IsGuardrail).ToList();
        var guardrails = results.Where(r => r.IsGuardrail).ToList();

        return new EvalReport(
            results.Count,
            HitRateAt3: retrieval.Count == 0 ? 1 : retrieval.Count(r => r.HitAt3) / (double)retrieval.Count,
            HitRateAt5: retrieval.Count == 0 ? 1 : retrieval.Count(r => r.HitAt5) / (double)retrieval.Count,
            Mrr: retrieval.Count == 0 ? 1 : retrieval.Average(r => r.ReciprocalRank),
            GuardrailAccuracy: guardrails.Count == 0 ? 1 : guardrails.Count(r => r.Faithfulness >= 1.0) / (double)guardrails.Count,
            AvgFaithfulness: retrieval.Count == 0 ? 1 : retrieval.Average(r => r.Faithfulness),
            results);
    }

    private async Task<(double Score, string Reason)> JudgeAsync(
        string context, string question, string answer, CancellationToken ct)
    {
        var prompt = JudgePrompt
            .Replace("{context}", context).Replace("{question}", question).Replace("{answer}", answer);
        try
        {
            var res = await openAi.GetChatClient(openAiOpts.Value.JudgeModel).CompleteChatAsync(
                [new UserChatMessage(prompt)],
                new ChatCompletionOptions
                {
                    Temperature = 0,
                    ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
                }, ct);

            using var json = JsonDocument.Parse(res.Value.Content[0].Text);
            return (json.RootElement.GetProperty("score").GetDouble(),
                    json.RootElement.GetProperty("reason").GetString() ?? "");
        }
        catch (Exception ex)
        {
            return RagPrompts.IsFallback(answer)
                ? (1.0, "Correctly fell back to refusal.")
                : (0.5, $"Judge parsing failed: {ex.Message}");
        }
    }
}