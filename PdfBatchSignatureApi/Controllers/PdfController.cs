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
    /// Opens an existing PDF at <paramref name="request"/>.PdfPath, stamps the given batch number
    /// and a signature image onto the configured page(s), and saves the result as a new PDF.
    /// The original PDF file is never modified.
    /// </summary>
    /// <param name="request">File paths (on the API host's file system) and the batch number to stamp.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <response code="200">The PDF was processed successfully.</response>
    /// <response code="400">The request was missing a required field or a field was invalid.</response>
    /// <response code="404">The PDF or signature image file does not exist.</response>
    /// <response code="422">The PDF or image exists but could not be processed (corrupt, invalid page number, etc).</response>
    /// <response code="500">An unexpected server error occurred.</response>
    [HttpPost("add-batch-signature")]
    [ProducesResponseType(typeof(AddBatchSignatureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> AddBatchSignature([FromBody] AddBatchSignatureRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(new ErrorResponse { Message = "Validation failed.", Errors = errors });
        }

        try
        {
            var result = await _pdfProcessingService.AddBatchAndSignatureAsync(request, cancellationToken);
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
