using RagApi.Models;

namespace RagApi.Services;

public static class RagPrompts
{
    public const string FallbackAnswer = "Nuk kam informacion për këtë në dokumentet e ngarkuara.";

    public const string SystemPrompt = """
        Ju jeni një asistent inteligjent i specializuar në përgjigjen e pyetjeve bazuar VETËM në dokumentet e ngarkuara.

        RREGULLA TË RREPTA (GUARDRAILS):
        1. BAZOHUNI VETËM NË KONTEKST: Përdorni ekskluzivisht informacionin e dhënë në [KONTEKST]. Mos supozoni ose përdorni njohuri të përgjithshme jashtë kontekstit.
        2. CITIME TË INTEGRUARA (INLINE CITATIONS): Për çdo pohim ose fakt që jepni në përgjigje, vendosni citimin përkatës në kllapa katrore, p.sh. [Burimi 1] ose [Burimi 2].
        3. RREGULLI I MOS-DIJES (FALLBACK): Nëse përgjigja nuk gjendet drejtpërdrejt në kontekst ose nëse të dhënat janë të pamjaftueshme, përgjigjuni VETËM me fjalinë ekzakte:
           'Nuk kam informacion për këtë në dokumentet e ngarkuara.'
           Mos ofroni përgjigje me hamendësim ose supozime.
        4. GJUHA: Përgjigjuni VETËM në gjuhën shqipe.
        """;

    public static string BuildContext(IReadOnlyList<RetrievedChunk> chunks) =>
        string.Join("\n\n", chunks.Select((c, i) =>
            $"[Burimi {i + 1}] (Dokumenti: {c.DocumentId} | Copëza #{c.ChunkId}):\n{c.Text}"));

    public static string BuildUserMessage(string context, string question) =>
        $"[KONTEKST]:\n{context}\n\n[PYETJA]:\n{question}\n\n[PËRGJIGJA ME CITIME INLINE]:";

    public static bool IsFallback(string answer) =>
        answer.Contains("nuk kam informacion", StringComparison.OrdinalIgnoreCase);
}