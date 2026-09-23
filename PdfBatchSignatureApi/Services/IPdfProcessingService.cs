using PdfBatchSignatureApi.Models;

namespace PdfBatchSignatureApi.Services;

/// <summary>
/// Adds a batch number and signature image overlay to an existing PDF, producing a new output file.
/// Implementations must not mutate the source PDF and must be safe for concurrent calls.
/// </summary>
public interface IPdfProcessingService
{
    /// <summary>
    /// Validates the inputs, opens the source PDF, stamps the batch number and signature onto the
    /// page(s) configured in appsettings.json, and writes the result to a new file in the output folder.
    /// <paramref name="pdfPath"/> and <paramref name="signatureImagePath"/> may be a bare file name
    /// (resolved under the configured allowed root folders) or a full path within them.
    /// <paramref name="batchNumberX"/>/<paramref name="batchNumberY"/> and
    /// <paramref name="signatureX"/>/<paramref name="signatureY"/> override the corresponding
    /// appsettings.json coordinate when supplied; a null value falls back to the configured default.
    /// </summary>
    /// <exception cref="PdfProcessingException">Thrown for any validation or processing failure.</exception>
    Task<AddBatchSignatureResponse> AddBatchAndSignatureAsync(
        string pdfPath,
        string signatureImagePath,
        string batchNumber,
        double? batchNumberX = null,
        double? batchNumberY = null,
        double? signatureX = null,
        double? signatureY = null,
        CancellationToken cancellationToken = default);
}
