using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.RegularExpressions;
using Paragraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using Table = DocumentFormat.OpenXml.Wordprocessing.Table;
using TableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using TableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;

namespace PathRAG.Core.Services;

public class DocumentExtractor : IDocumentExtractor
{
    private readonly ILogger<DocumentExtractor> _logger;
    private readonly PathRagOptions _options;
    private readonly Dictionary<string, Func<Stream, CancellationToken, Task<string>>> _extractors;

    public DocumentExtractor(
        ILogger<DocumentExtractor> logger,
        IOptions<PathRagOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        _extractors = new Dictionary<string, Func<Stream, CancellationToken, Task<string>>>(
            StringComparer.OrdinalIgnoreCase)
        {
            { ".txt", ExtractFromPlainTextAsync },
            { ".pdf", ExtractFromPdfAsync },
            { ".docx", ExtractFromDocxAsync },
            { ".doc", ExtractFromDocxAsync },
            { ".xlsx", ExtractFromXlsxAsync },
            { ".xls", ExtractFromXlsxAsync },
            { ".html", ExtractFromHtmlAsync },
            { ".htm", ExtractFromHtmlAsync },
            { ".md", ExtractFromMarkdownAsync },
            { ".json", ExtractFromPlainTextAsync },
            { ".xml", ExtractFromPlainTextAsync },
            { ".csv", ExtractFromPlainTextAsync },
            { ".rtf", ExtractFromRtfAsync }
        };
    }

    public bool IsSupported(string fileExtension)
    {
        return _extractors.ContainsKey(fileExtension);
    }

    public async Task<string> ExtractTextAsync(Stream stream, string fileExtension, CancellationToken cancellationToken = default)
    {
        if (!IsSupported(fileExtension))
        {
            throw new NotSupportedException($"File extension {fileExtension} is not supported");
        }

        try
        {
            var text = await _extractors[fileExtension](stream, cancellationToken);
            return NormalizeText(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from file with extension {FileExtension}", fileExtension);
            throw;
        }
    }

    private async Task<string> ExtractFromPlainTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private async Task<string> ExtractFromPdfAsync(Stream stream, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        using var pdfReader = new PdfReader(stream);
        using var pdfDocument = new PdfDocument(pdfReader);

        var listener = new LocationTextExtractionStrategy();

        for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = pdfDocument.GetPage(i);
            text.AppendLine(PdfTextExtractor.GetTextFromPage(page, listener));
        }

        return text.ToString();
    }

    private async Task<string> ExtractFromXlsxAsync(Stream stream, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        using var workbook = new XLWorkbook(stream);

        foreach (var worksheet in workbook.Worksheets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            text.AppendLine($"Sheet: {worksheet.Name}");

            var usedRange = worksheet.RangeUsed();
            if (usedRange != null)
            {
                foreach (var row in usedRange.RowsUsed())
                {
                    var cellValues = row.CellsUsed()
                        .Select(cell => cell.GetFormattedString())
                        .Where(value => !string.IsNullOrWhiteSpace(value));

                    if (cellValues.Any())
                    {
                        text.AppendLine(string.Join(" | ", cellValues));
                    }
                }
            }
            text.AppendLine();
        }

        return text.ToString();
    }

    private async Task<string> ExtractFromDocxAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var mainPart = doc.MainDocumentPart;
        if (mainPart?.Document?.Body == null)
        {
            return string.Empty;
        }

        var text = new StringBuilder();

        // Extract main document text
        foreach (var para in mainPart.Document.Body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(para.InnerText))
            {
                text.AppendLine(para.InnerText.Trim());
            }
        }

        // Extract text from tables
        var tables = mainPart.Document.Body.Descendants<Table>();
        foreach (var table in tables)
        {
            text.AppendLine("Table content:");
            foreach (var row in table.Descendants<TableRow>())
            {
                var cellTexts = row.Descendants<TableCell>()
                    .Select(cell => cell.InnerText.Trim())
                    .Where(cellText => !string.IsNullOrWhiteSpace(cellText));

                if (cellTexts.Any())
                {
                    text.AppendLine(string.Join(" | ", cellTexts));
                }
            }
            text.AppendLine();
        }

        // Extract text from headers
        foreach (var headerPart in mainPart.HeaderParts)
        {
            text.AppendLine("Header:");
            foreach (var para in headerPart.Header.Descendants<Paragraph>())
            {
                if (!string.IsNullOrWhiteSpace(para.InnerText))
                {
                    text.AppendLine(para.InnerText.Trim());
                }
            }
        }

        // Extract text from footers
        foreach (var footerPart in mainPart.FooterParts)
        {
            text.AppendLine("Footer:");
            foreach (var para in footerPart.Footer.Descendants<Paragraph>())
            {
                if (!string.IsNullOrWhiteSpace(para.InnerText))
                {
                    text.AppendLine(para.InnerText.Trim());
                }
            }
        }

        return text.ToString();
    }

    private async Task<string> ExtractFromHtmlAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync(cancellationToken);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Remove script and style elements
        var nodes = doc.DocumentNode.SelectNodes("//script|//style");
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                node.Remove();
            }
        }

        return doc.DocumentNode.InnerText;
    }

    private async Task<string> ExtractFromMarkdownAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var markdown = await reader.ReadToEndAsync(cancellationToken);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var html = Markdown.ToHtml(markdown, pipeline);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.InnerText;
    }

    private async Task<string> ExtractFromRtfAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var rtf = await reader.ReadToEndAsync(cancellationToken);

        // Simple RTF to text conversion
        var text = Regex.Replace(rtf, @"[\{\}\\\n]|<[^>]*>|\b\w+\b(?=:)", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Replace multiple whitespace characters with a single space
        text = Regex.Replace(text, @"\s+", " ");

        // Remove any non-printable characters
        text = Regex.Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Trim leading/trailing whitespace
        return text.Trim();
    }
}