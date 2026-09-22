using PdfBatchSignatureApi.Models;

namespace PdfBatchSignatureApi.Services;

/// <summary>
/// Adds a batch number and signature image overlay to an existing PDF, producing a new output file.
/// Implementations must not mutate the source PDF and must be safe for concurrent calls.
/// </summary>
public interface IPdfProcessingService
{
    /// <summary>
    /// Validates the request, opens the source PDF, stamps the batch number and signature
    /// onto the configured page(s), and writes the result to a new file in the output folder.
    /// </summary>
    /// <exception cref="PdfProcessingException">Thrown for any validation or processing failure.</exception>
    Task<AddBatchSignatureResponse> AddBatchAndSignatureAsync(AddBatchSignatureRequest request, CancellationToken cancellationToken = default);
}
