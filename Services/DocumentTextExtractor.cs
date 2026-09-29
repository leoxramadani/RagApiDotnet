using System;
using System.Data;
using System.IO;
using System.Text;
using RagApi.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using ExcelDataReader;
namespace RagApi.Services
{
	public interface IDocumentTextExtractor
	{
		Task<ExtractedDocument> ExtractAsync(IFormFile file, CancellationToken ctx);
	}

	public class DocumentTextExtractor : IDocumentTextExtractor
    {
        public static readonly string[] SupportedExtensions = [".pdf", ".txt", ".xlsx", ".xls"];

		public async Task<ExtractedDocument> ExtractAsync(IFormFile file, CancellationToken ctx)
		{
			var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
			using var ms = new MemoryStream();
			await file.CopyToAsync(ms, ctx);
			ms.Position = 0;

			return ext switch
			{
				".pdf" => new ExtractedDocument(ExtractPdf(ms), null),
				".xlsx" or ".xls" => ExtractExcel(ms),
				".txt" => new ExtractedDocument(DecodeText(ms.ToArray()), null),
				_ => throw new NotSupportedException($"Unsupported file type '{ext}'.")
			};
        }

		private static string ExtractPdf(Stream stream)
		{
			using var pdf = PdfDocument.Open(stream);
			var pages = new List<string>();
			foreach (var page in pdf.GetPages())
			{
				var text = ContentOrderTextExtractor.GetText(page);
				if (!string.IsNullOrWhiteSpace(text))
					pages.Add($"--- Faqja {page.Number} --- \n{text}");
			}
			return string.Join("\n\n", pages);
		}

        private static ExtractedDocument ExtractExcel(Stream stream)
        {
            using var reader = ExcelReaderFactory.CreateReader(stream);
            var ds = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
            });
            var table = ds.Tables[0];
            var chunks = new List<string>();

            for (var i = 0; i < table.Rows.Count; i++)
            {
                var sb = new StringBuilder($"Record {i + 1}:\n");
                foreach (DataColumn col in table.Columns)
                {
                    var value = table.Rows[i][col];
                    if (value is not DBNull && !string.IsNullOrWhiteSpace(value.ToString()))
                        sb.AppendLine($"{col.ColumnName}: {value}");
                }
                chunks.Add(sb.ToString().Trim());
            }
            return new ExtractedDocument(string.Join("\n\n", chunks), chunks);
        }

        private static string DecodeText(byte[] bytes)
        {
            try { return new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
        }
    }
}