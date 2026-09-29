using System;
namespace RagApi.Services
{
    public class RecursiveTextSplitter
    {
        private static readonly string[] Separators = {"\n\n", "\n", ". ", " ", ""};

        public IReadOnlyList<string> Split(string text, int chunkSize, int overlap)
        {
            if (overlap >= chunkSize) throw new ArgumentException("Overlap must be smaller than chunk size");
            return SplitRecursive(text, Separators, chunkSize, overlap);
        }

        private static List<string> SplitRecursive(string text, string[] separators, int chunkSize, int overlap)
        {
            var separator = "";
            var remaining = Array.Empty<string>();

            for(var i = 0; i < separators.Length; i++)
            {
                if (separators[i] == "") { separator = ""; break; }
                if (text.Contains(separators[i]))
                {
                    separator = separators[i];
                    remaining = separators[(i + 1)..];
                    break;
                }
            }

            var splits = (separator == "" ? text.Select(x => x.ToString()) : text.Split(separator)).Where(x => x.Length > 0).ToList();

            var result = new List<string>();
            var good = new List<string>();

            foreach (var s in splits)
            {
                if(s.Length < chunkSize) { good.Add(s); continue; }
                if(good.Count > 0)
                {
                    result.AddRange(Merge(good, separator, chunkSize, overlap));
                    good.Clear();
                }
                if (remaining.Length == 0) result.Add(s);
                else result.AddRange(SplitRecursive(s, remaining, chunkSize, overlap));
            }
            if (good.Count > 0) result.AddRange(Merge(good, separator, chunkSize, overlap));
            return result;
        }

        private static List<string> Merge(List<string> splits, string separator, int chunkSize, int overlap)
        {
            var docs = new List<string>();
            var current = new List<string>();
            var total = 0;
            var sepLen = separator.Length;

            foreach (var s in splits)
            {
                if(current.Count > 0 && total + s.Length + sepLen > chunkSize)
                {
                    AddDoc(docs, current, separator);
                    while (total > overlap ||
                                           (total > 0 && total + s.Length + (current.Count > 0 ? sepLen : 0) > chunkSize))
                    {
                        total -= current[0].Length + (current.Count > 1 ? sepLen : 0);
                        current.RemoveAt(0);
                    }
                }
                current.Add(s);
                total += s.Length + (current.Count > 1 ? sepLen : 0);
            }
            AddDoc(docs, current, separator);
            return docs;
        }

        private static void AddDoc(List<string> docs, List<string> current, string separator)
        {
            var doc = string.Join(separator, current).Trim();
            if (doc.Length > 0) docs.Add(doc);
        }

    }
}

