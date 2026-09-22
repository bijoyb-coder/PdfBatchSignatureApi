using System.ComponentModel.DataAnnotations;

namespace PdfBatchSignatureApi.Models;

/// <summary>
/// Request payload for adding a batch number and signature image to an existing PDF.
/// All paths must be absolute file system paths reachable by the API host, and must
/// fall within the configured allowed root folders (see AllowedPdfRoot / AllowedSignatureRoot).
/// </summary>
public class AddBatchSignatureRequest
{
    /// <summary>Absolute path to the existing source PDF file. Must have a .pdf extension.</summary>
    [Required(ErrorMessage = "PdfPath is required.")]
    public string PdfPath { get; set; } = string.Empty;

    /// <summary>Absolute path to the signature image file. Must be .png, .jpg, or .jpeg.</summary>
    [Required(ErrorMessage = "SignatureImagePath is required.")]
    public string SignatureImagePath { get; set; } = string.Empty;

    /// <summary>Batch number to stamp onto the PDF. Trimmed; max 100 characters.</summary>
    [Required(ErrorMessage = "BatchNumber is required.")]
    [StringLength(100, ErrorMessage = "BatchNumber cannot exceed 100 characters.")]
    public string BatchNumber { get; set; } = string.Empty;
}
