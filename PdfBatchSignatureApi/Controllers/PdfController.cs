using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PdfBatchSignatureApi.Models;
using PdfBatchSignatureApi.Services;

namespace PdfBatchSignatureApi.Controllers;

/// <summary>Endpoints for stamping a batch number and signature image onto an existing PDF.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PdfController : ControllerBase
{
    private readonly IPdfProcessingService _pdfProcessingService;
    private readonly ILogger<PdfController> _logger;

    public PdfController(IPdfProcessingService pdfProcessingService, ILogger<PdfController> logger)
    {
        _pdfProcessingService = pdfProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Opens an existing PDF, stamps the given batch number and a signature image onto the page(s)
    /// configured in appsettings.json, and saves the result as a new PDF. The original PDF file is
    /// never modified.
    /// </summary>
    /// <param name="pdfPath">
    /// File name of the existing source PDF (e.g. "Quotation_10025.pdf"), looked up in the server's
    /// configured PdfProcessing:AllowedPdfRoot folder. A full path is also accepted as long as it
    /// resolves inside that folder. Must have a .pdf extension.
    /// </param>
    /// <param name="signatureImagePath">
    /// File name of the signature image (e.g. "AuthorizedSignature.png"), looked up in the server's
    /// configured PdfProcessing:AllowedSignatureRoot folder. A full path is also accepted as long as
    /// it resolves inside that folder. Must be .png, .jpg, or .jpeg.
    /// </param>
    /// <param name="batchNumber">Batch number to stamp onto the PDF. Trimmed; max 100 characters.</param>
    /// <param name="batchNumberX">
    /// Optional. X coordinate (in points, from the left edge of the page) to draw the batch number at.
    /// Omit to use the fixed PdfProcessing:BatchNumber:X value from appsettings.json.
    /// </param>
    /// <param name="batchNumberY">
    /// Optional. Y coordinate (in points, from the top edge of the page) to draw the batch number at.
    /// Omit to use the fixed PdfProcessing:BatchNumber:Y value from appsettings.json.
    /// </param>
    /// <param name="signatureX">
    /// Optional. X coordinate (in points, from the left edge of the page) to draw the signature image at.
    /// Omit to use the fixed PdfProcessing:Signature:X value from appsettings.json.
    /// </param>
    /// <param name="signatureY">
    /// Optional. Y coordinate (in points, from the top edge of the page) to draw the signature image at.
    /// Omit to use the fixed PdfProcessing:Signature:Y value from appsettings.json.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <response code="200">The PDF was processed successfully.</response>
    /// <response code="400">A required field was missing or invalid.</response>
    /// <response code="404">The PDF or signature image file does not exist.</response>
    /// <response code="422">The PDF or image exists but could not be processed (corrupt, invalid page number, etc).</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPost("add-batch-signature")]
    [ProducesResponseType(typeof(AddBatchSignatureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddBatchSignature(
        [FromQuery, Required(ErrorMessage = "pdfPath is required.")] string pdfPath,
        [FromQuery, Required(ErrorMessage = "signatureImagePath is required.")] string signatureImagePath,
        [FromQuery, Required(ErrorMessage = "batchNumber is required."), StringLength(100, ErrorMessage = "batchNumber cannot exceed 100 characters.")] string batchNumber,
        [FromQuery] double? batchNumberX,
        [FromQuery] double? batchNumberY,
        [FromQuery] double? signatureX,
        [FromQuery] double? signatureY,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ErrorResponse { Message = "Validation failed.", Errors = errors });
        }

        try
        {
            var result = await _pdfProcessingService.AddBatchAndSignatureAsync(
                pdfPath, signatureImagePath, batchNumber,
                batchNumberX, batchNumberY, signatureX, signatureY,
                cancellationToken);
            return Ok(result);
        }
        catch (PdfProcessingException ex)
        {
            _logger.LogWarning(ex, "PDF processing failed with {ErrorType}: {Message}", ex.ErrorType, ex.Message);
            var errorResponse = new ErrorResponse { Message = ex.Message };
            return ex.ErrorType switch
            {
                PdfProcessingErrorType.Validation => BadRequest(errorResponse),
                PdfProcessingErrorType.NotFound => NotFound(errorResponse),
                PdfProcessingErrorType.Unprocessable => UnprocessableEntity(errorResponse),
                _ => StatusCode(StatusCodes.Status500InternalServerError, errorResponse)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing PDF request.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse { Message = "An unexpected error occurred while processing the PDF." });
        }
    }
}
