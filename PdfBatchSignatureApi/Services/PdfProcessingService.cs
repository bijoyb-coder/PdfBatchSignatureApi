using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PdfBatchSignatureApi.Configuration;
using PdfBatchSignatureApi.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfBatchSignatureApi.Services;

/// <summary>
/// Opens an existing PDF with PDFsharp, overlays a batch number text and a signature image
/// on the configured page(s), and saves the result as a new file. Stateless and safe for
/// concurrent requests: all state lives on the call stack, and output filenames are unique per call.
/// </summary>
public class PdfProcessingService : IPdfProcessingService
{
    private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg" };
    private readonly PdfProcessingOptions _options;
    private readonly ILogger<PdfProcessingService> _logger;

    public PdfProcessingService(IOptions<PdfProcessingOptions> options, ILogger<PdfProcessingService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AddBatchSignatureResponse> AddBatchAndSignatureAsync(AddBatchSignatureRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchNumber = request.BatchNumber.Trim();

        _logger.LogInformation(
            "Processing request received. PdfPath={PdfPath} SignatureImagePath={SignatureImagePath} BatchNumber={BatchNumber}",
            request.PdfPath, request.SignatureImagePath, batchNumber);

        var pdfPath = ValidateAndResolvePath(request.PdfPath, _options.AllowedPdfRoot, new[] { ".pdf" }, "PdfPath");
        var imagePath = ValidateAndResolvePath(request.SignatureImagePath, _options.AllowedSignatureRoot, AllowedImageExtensions, "SignatureImagePath");

        if (!File.Exists(pdfPath))
        {
            throw new PdfProcessingException(PdfProcessingErrorType.NotFound, $"PDF file was not found: {request.PdfPath}");
        }

        if (!File.Exists(imagePath))
        {
            throw new PdfProcessingException(PdfProcessingErrorType.NotFound, $"Signature image file was not found: {request.SignatureImagePath}");
        }

        Directory.CreateDirectory(_options.OutputFolder);

        var outputPath = BuildOutputPath(pdfPath, batchNumber);

        var batchSettings = MergeBatchNumberSettings(_options.BatchNumber, request.BatchNumberPosition);
        var signatureSettings = MergeSignatureSettings(_options.Signature, request.SignaturePosition);

        await Task.Run(() => ProcessPdf(pdfPath, imagePath, batchNumber, outputPath, batchSettings, signatureSettings), cancellationToken);

        stopwatch.Stop();
        _logger.LogInformation(
            "PDF processing completed. OutputPath={OutputPath} DurationMs={DurationMs}",
            outputPath, stopwatch.ElapsedMilliseconds);

        return new AddBatchSignatureResponse
        {
            Success = true,
            Message = "PDF processed successfully.",
            BatchNumber = batchNumber,
            InputPdf = pdfPath,
            SignatureImage = imagePath,
            OutputPdf = outputPath
        };
    }

    private void ProcessPdf(string pdfPath, string imagePath, string batchNumber, string outputPath, BatchNumberSettings batchSettings, SignatureSettings signatureSettings)
    {
        _logger.LogInformation("PDF processing started for {PdfPath}", pdfPath);

        PdfDocument document;
        try
        {
            document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify);
        }
        catch (Exception ex) when (ex is not PdfProcessingException)
        {
            _logger.LogError(ex, "Failed to open PDF {PdfPath}", pdfPath);
            throw new PdfProcessingException(PdfProcessingErrorType.Unprocessable, "The PDF file could not be opened. It may be corrupted, encrypted, or not a valid PDF.", ex);
        }

        using (document)
        {
            var pageCount = document.PageCount;

            ValidatePageNumber(batchSettings.PageNumber, pageCount, "BatchNumberPosition.PageNumber");
            ValidatePageNumber(signatureSettings.PageNumber, pageCount, "SignaturePosition.PageNumber");

            XImage signatureImage;
            try
            {
                signatureImage = XImage.FromFile(imagePath);
            }
            catch (Exception ex) when (ex is not PdfProcessingException)
            {
                _logger.LogError(ex, "Failed to load signature image {ImagePath}", imagePath);
                throw new PdfProcessingException(PdfProcessingErrorType.Unprocessable, "The signature image could not be loaded. It may be corrupted or in an unsupported format.", ex);
            }

            using (signatureImage)
            {
                DrawBatchNumber(document, batchNumber, batchSettings);
                DrawSignature(document, signatureImage, signatureSettings);

                try
                {
                    document.Save(outputPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save output PDF {OutputPath}", outputPath);
                    throw new PdfProcessingException(PdfProcessingErrorType.Unprocessable, "The modified PDF could not be saved.", ex);
                }
            }
        }
    }

    /// <summary>Combines the configured defaults with any non-null fields from the per-request override.</summary>
    private static BatchNumberSettings MergeBatchNumberSettings(BatchNumberSettings defaults, BatchNumberPositionOverride? overrideValues)
    {
        if (overrideValues is null)
        {
            return defaults;
        }

        return new BatchNumberSettings
        {
            PageNumber = overrideValues.PageNumber ?? defaults.PageNumber,
            X = overrideValues.X ?? defaults.X,
            Y = overrideValues.Y ?? defaults.Y,
            FontSize = overrideValues.FontSize ?? defaults.FontSize,
            FontName = overrideValues.FontName ?? defaults.FontName,
            Width = overrideValues.Width ?? defaults.Width,
            Height = overrideValues.Height ?? defaults.Height
        };
    }

    /// <summary>Combines the configured defaults with any non-null fields from the per-request override.</summary>
    private static SignatureSettings MergeSignatureSettings(SignatureSettings defaults, SignaturePositionOverride? overrideValues)
    {
        if (overrideValues is null)
        {
            return defaults;
        }

        return new SignatureSettings
        {
            PageNumber = overrideValues.PageNumber ?? defaults.PageNumber,
            X = overrideValues.X ?? defaults.X,
            Y = overrideValues.Y ?? defaults.Y,
            Width = overrideValues.Width ?? defaults.Width,
            Height = overrideValues.Height ?? defaults.Height
        };
    }

    private void DrawBatchNumber(PdfDocument document, string batchNumber, BatchNumberSettings settings)
    {
        var page = document.Pages[settings.PageNumber - 1];

        using var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont(settings.FontName, settings.FontSize, XFontStyleEx.Regular);
        var brush = XBrushes.Black;

        // (0,0) is the top-left corner of the page in PDFsharp's default XGraphics space; Y grows downward.
        if (settings.Width.HasValue && settings.Height.HasValue)
        {
            var rect = new XRect(settings.X, settings.Y, settings.Width.Value, settings.Height.Value);
            gfx.DrawString(batchNumber, font, brush, rect, XStringFormats.TopLeft);
        }
        else
        {
            gfx.DrawString(batchNumber, font, brush, new XPoint(settings.X, settings.Y));
        }
    }

    private void DrawSignature(PdfDocument document, XImage signatureImage, SignatureSettings settings)
    {
        var page = document.Pages[settings.PageNumber - 1];

        using var gfx = XGraphics.FromPdfPage(page);
        var rect = new XRect(settings.X, settings.Y, settings.Width, settings.Height);
        gfx.DrawImage(signatureImage, rect);
    }

    private static void ValidatePageNumber(int pageNumber, int pageCount, string label)
    {
        if (pageNumber < 1 || pageNumber > pageCount)
        {
            throw new PdfProcessingException(
                PdfProcessingErrorType.Unprocessable,
                $"{label} page number {pageNumber} is invalid. The PDF has {pageCount} page(s).");
        }
    }

    /// <summary>
    /// Resolves a user-supplied path to a full path and verifies it lies within the configured
    /// allowed root directory, preventing path traversal outside trusted folders.
    /// </summary>
    private static string ValidateAndResolvePath(string suppliedPath, string allowedRoot, string[] allowedExtensions, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(suppliedPath))
        {
            throw new PdfProcessingException(PdfProcessingErrorType.Validation, $"{fieldName} is required.");
        }

        var extension = Path.GetExtension(suppliedPath);
        if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new PdfProcessingException(
                PdfProcessingErrorType.Validation,
                $"{fieldName} has an unsupported extension '{extension}'. Allowed: {string.Join(", ", allowedExtensions)}.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(suppliedPath);
        }
        catch (Exception ex)
        {
            throw new PdfProcessingException(PdfProcessingErrorType.Validation, $"{fieldName} is not a valid file system path.", ex);
        }

        if (string.IsNullOrWhiteSpace(allowedRoot))
        {
            return fullPath;
        }

        var fullAllowedRoot = Path.GetFullPath(allowedRoot);
        var normalizedRoot = fullAllowedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfProcessingException(
                PdfProcessingErrorType.Validation,
                $"{fieldName} must be located within the allowed folder '{fullAllowedRoot}'.");
        }

        return fullPath;
    }

    /// <summary>
    /// Builds the output filename as {name}_Batch_{sanitizedBatch}_Signed.pdf. If a file with that
    /// name already exists (e.g. a concurrent or repeated request), a short unique suffix is appended
    /// so no request ever overwrites another request's output.
    /// </summary>
    private string BuildOutputPath(string pdfPath, string batchNumber)
    {
        var baseName = Path.GetFileNameWithoutExtension(pdfPath);
        var safeBatch = SanitizeForFileName(batchNumber);
        var fileName = $"{baseName}_Batch_{safeBatch}_Signed.pdf";
        var fullPath = Path.Combine(_options.OutputFolder, fileName);

        if (File.Exists(fullPath))
        {
            var uniqueSuffix = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}"[..21];
            fileName = $"{baseName}_Batch_{safeBatch}_Signed_{uniqueSuffix}.pdf";
            fullPath = Path.Combine(_options.OutputFolder, fileName);
        }

        return fullPath;
    }

    private static readonly Regex UnsafeFileNameChars = new(@"[^a-zA-Z0-9\-_]", RegexOptions.Compiled);

    private static string SanitizeForFileName(string value)
    {
        var sanitized = UnsafeFileNameChars.Replace(value, "_");
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }
}
