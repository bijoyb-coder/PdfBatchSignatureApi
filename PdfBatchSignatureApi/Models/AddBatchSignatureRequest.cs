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

    /// <summary>
    /// Optional per-request placement for the batch number text. Any field left null falls back to
    /// the corresponding <c>PdfProcessing:BatchNumber</c> value in appsettings.json. Use this when
    /// different PDF templates need the batch number in different spots on the same call.
    /// </summary>
    public BatchNumberPositionOverride? BatchNumberPosition { get; set; }

    /// <summary>
    /// Optional per-request placement for the signature image. Any field left null falls back to
    /// the corresponding <c>PdfProcessing:Signature</c> value in appsettings.json. Use this when
    /// different PDF templates need the signature in a different spot/size on the same call.
    /// </summary>
    public SignaturePositionOverride? SignaturePosition { get; set; }
}

/// <summary>Per-request override for where/how the batch number is drawn. All fields optional.</summary>
public class BatchNumberPositionOverride
{
    /// <summary>1-based page number. Overrides <c>PdfProcessing:BatchNumber:PageNumber</c> if set.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "BatchNumberPosition.PageNumber must be 1 or greater.")]
    public int? PageNumber { get; set; }

    /// <summary>X coordinate in points from the left edge of the page.</summary>
    public double? X { get; set; }

    /// <summary>Y coordinate in points from the top edge of the page.</summary>
    public double? Y { get; set; }

    /// <summary>Font size in points.</summary>
    [Range(1, 400, ErrorMessage = "BatchNumberPosition.FontSize must be between 1 and 400.")]
    public double? FontSize { get; set; }

    /// <summary>Font family name (see README for supported fonts).</summary>
    public string? FontName { get; set; }

    /// <summary>Optional bounding box width in points.</summary>
    public double? Width { get; set; }

    /// <summary>Optional bounding box height in points.</summary>
    public double? Height { get; set; }
}

/// <summary>Per-request override for where/how the signature image is drawn. All fields optional.</summary>
public class SignaturePositionOverride
{
    /// <summary>1-based page number. Overrides <c>PdfProcessing:Signature:PageNumber</c> if set.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "SignaturePosition.PageNumber must be 1 or greater.")]
    public int? PageNumber { get; set; }

    /// <summary>X coordinate in points from the left edge of the page.</summary>
    public double? X { get; set; }

    /// <summary>Y coordinate in points from the top edge of the page.</summary>
    public double? Y { get; set; }

    /// <summary>Width of the drawn signature image in points.</summary>
    [Range(1, 5000, ErrorMessage = "SignaturePosition.Width must be greater than 0.")]
    public double? Width { get; set; }

    /// <summary>Height of the drawn signature image in points.</summary>
    [Range(1, 5000, ErrorMessage = "SignaturePosition.Height must be greater than 0.")]
    public double? Height { get; set; }
}
