namespace PdfBatchSignatureApi.Services;

/// <summary>Category of failure, used by the controller to select the HTTP status code.</summary>
public enum PdfProcessingErrorType
{
    /// <summary>Bad input (maps to 400).</summary>
    Validation,

    /// <summary>Referenced file does not exist or is outside an allowed folder (maps to 404).</summary>
    NotFound,

    /// <summary>File exists but could not be processed, e.g. corrupt PDF/image or bad page number (maps to 422).</summary>
    Unprocessable
}

/// <summary>Thrown by <see cref="IPdfProcessingService"/> for any expected/handled failure.</summary>
public class PdfProcessingException : Exception
{
    public PdfProcessingErrorType ErrorType { get; }

    public PdfProcessingException(PdfProcessingErrorType errorType, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorType = errorType;
    }
}
