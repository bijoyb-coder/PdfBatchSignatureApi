namespace PdfBatchSignatureApi.Models;

/// <summary>Response returned after successfully processing a PDF.</summary>
public class AddBatchSignatureResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public string InputPdf { get; set; } = string.Empty;
    public string SignatureImage { get; set; } = string.Empty;
    public string OutputPdf { get; set; } = string.Empty;
}

/// <summary>Standard error response body.</summary>
public class ErrorResponse
{
    public bool Success { get; set; } = false;
    public string Message { get; set; } = string.Empty;
    public List<string>? Errors { get; set; }
}
