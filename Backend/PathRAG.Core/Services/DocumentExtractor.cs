using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using HtmlAgilityPack;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using VersOne.Epub;
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
            { ".rtf", ExtractFromRtfAsync },
            { ".pptx", ExtractFromPptxAsync },
            { ".ppt", ExtractFromPptxAsync },
            { ".epub", ExtractFromEpubAsync },
            { ".odt", ExtractFromOdtAsync }
        };
    }

    public bool IsSupported(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        string fileExtension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(fileExtension) && _extractors.ContainsKey(fileExtension);
    }

    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!IsSupported(fileName))
        {
            throw new NotSupportedException($"File {fileName} is not supported");
        }

        string fileExtension = Path.GetExtension(fileName);

        try
        {
            _logger.LogInformation("Extracting text from {FileName} with extension {FileExtension}", fileName, fileExtension);
            var text = await _extractors[fileExtension](stream, cancellationToken);
            return NormalizeText(text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from file {FileName} with extension {FileExtension}", fileName, fileExtension);
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

    private async Task<string> ExtractFromPptxAsync(Stream stream, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        using var presentationDoc = PresentationDocument.Open(stream, false);
        var presentationPart = presentationDoc.PresentationPart;

        if (presentationPart == null)
        {
            return string.Empty;
        }

        var presentation = presentationPart.Presentation;
        var slideIdList = presentation.SlideIdList;

        if (slideIdList == null)
        {
            return string.Empty;
        }

        // Process each slide
        foreach (var slideId in slideIdList.ChildElements.OfType<SlideId>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var slidePartRelationshipId = slideId.RelationshipId;
            if (slidePartRelationshipId == null)
            {
                continue;
            }

            var slidePart = presentationPart.GetPartById(slidePartRelationshipId) as SlidePart;
            if (slidePart == null)
            {
                continue;
            }

            // Extract text from slide
            text.AppendLine($"Slide {slideId.Id}:");

            // Get all text elements in the slide
            var paragraphs = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>();
            foreach (var paragraph in paragraphs)
            {
                var runs = paragraph.Descendants<DocumentFormat.OpenXml.Drawing.Run>();
                foreach (var run in runs)
                {
                    var textElements = run.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                    foreach (var textElement in textElements)
                    {
                        if (!string.IsNullOrWhiteSpace(textElement.Text))
                        {
                            text.AppendLine(textElement.Text.Trim());
                        }
                    }
                }
            }

            // Extract text from notes if available
            if (slidePart.NotesSlidePart != null)
            {
                text.AppendLine("Notes:");
                var notesParagraphs = slidePart.NotesSlidePart.NotesSlide.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>();
                foreach (var paragraph in notesParagraphs)
                {
                    var runs = paragraph.Descendants<DocumentFormat.OpenXml.Drawing.Run>();
                    foreach (var run in runs)
                    {
                        var textElements = run.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
                        foreach (var textElement in textElements)
                        {
                            if (!string.IsNullOrWhiteSpace(textElement.Text))
                            {
                                text.AppendLine(textElement.Text.Trim());
                            }
                        }
                    }
                }
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    private async Task<string> ExtractFromEpubAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            // Create a temporary file to save the stream content
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var fileStream = File.Create(tempFilePath))
                {
                    await stream.CopyToAsync(fileStream, cancellationToken);
                }
                var text = new StringBuilder();
                using (EpubBookRef bookRef = EpubReader.OpenBook(stream))
                {
                    foreach (var readingOrder in bookRef.GetReadingOrder())
                    {
                        string content = readingOrder.ReadContentAsText();
                        text.AppendLine(content);
                    }
                }
                

                return text.ToString();
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from EPUB file");
            throw;
        }
    }

    private static void PrintNavigationItem(EpubNavigationItemRef navigationItemRef, int identLevel)
    {
        Console.Write(new string(' ', identLevel * 2));
        Console.WriteLine(navigationItemRef.Title);
        foreach (EpubNavigationItemRef nestedNavigationItemRef in navigationItemRef.NestedItems)
        {
            PrintNavigationItem(nestedNavigationItemRef, identLevel + 1);
        }
    }

    private async Task<string> ExtractFromOdtAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var text = new StringBuilder();

            // Create a temporary file to save the stream content
            var tempFilePath = Path.GetTempFileName();
            try
            {
                using (var fileStream = File.Create(tempFilePath))
                {
                    await stream.CopyToAsync(fileStream, cancellationToken);
                }

                // ODT files are ZIP archives with XML content
                using (var archive = ZipFile.OpenRead(tempFilePath))
                {
                    // The main content is in content.xml
                    var contentEntry = archive.GetEntry("content.xml");
                    if (contentEntry != null)
                    {
                        using var contentStream = contentEntry.Open();
                        using var reader = new StreamReader(contentStream);
                        var xmlContent = await reader.ReadToEndAsync(cancellationToken);

                        // Parse the XML content
                        var xmlDoc = new XmlDocument();
                        xmlDoc.LoadXml(xmlContent);

                        // Extract text from text:p elements (paragraphs)
                        var nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
                        nsManager.AddNamespace("text", "urn:oasis:names:tc:opendocument:xmlns:text:1.0");

                        var paragraphs = xmlDoc.SelectNodes("//text:p", nsManager);
                        if (paragraphs != null)
                        {
                            foreach (XmlNode paragraph in paragraphs)
                            {
                                cancellationToken.ThrowIfCancellationRequested();

                                if (!string.IsNullOrWhiteSpace(paragraph.InnerText))
                                {
                                    text.AppendLine(paragraph.InnerText.Trim());
                                }
                            }
                        }
                    }
                }

                return text.ToString();
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from ODT file");
            throw;
        }
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