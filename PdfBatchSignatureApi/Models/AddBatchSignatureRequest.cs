using System.ComponentModel.DataAnnotations;

namespace PdfBatchSignatureApi.Models;

/// <summary>
/// Request payload for adding a batch number and signature image to an existing PDF.
/// <para>
/// <see cref="PdfPath"/> and <see cref="SignatureImagePath"/> are normally just a **file name**
/// (e.g. <c>"Quotation_10025.pdf"</c>) — exactly what a UI text box would collect — which is resolved
/// automatically under the server's configured <c>PdfProcessing:AllowedPdfRoot</c> /
/// <c>AllowedSignatureRoot</c> folders. A full path is also accepted, but must still resolve inside
/// those same allowed folders.
/// </para>
/// </summary>
public class AddBatchSignatureRequest
{
    /// <summary>
    /// File name of the existing source PDF (e.g. <c>"Quotation_10025.pdf"</c>), looked up in the
    /// server's configured <c>PdfProcessing:AllowedPdfRoot</c> folder. A full path is also accepted
    /// as long as it resolves inside that folder. Must have a .pdf extension.
    /// </summary>
    [Required(ErrorMessage = "PdfPath is required.")]
    public string PdfPath { get; set; } = string.Empty;

    /// <summary>
    /// File name of the signature image (e.g. <c>"AuthorizedSignature.png"</c>), looked up in the
    /// server's configured <c>PdfProcessing:AllowedSignatureRoot</c> folder. A full path is also
    /// accepted as long as it resolves inside that folder. Must be .png, .jpg, or .jpeg.
    /// </summary>
    [Required(ErrorMessage = "SignatureImagePath is required.")]
    public string SignatureImagePath { get; set; } = string.Empty;

    /// <summary>Batch number to stamp onto the PDF. Trimmed; max 100 characters.</summary>
    [Required(ErrorMessage = "BatchNumber is required.")]
    [StringLength(100, ErrorMessage = "BatchNumber cannot exceed 100 characters.")]
    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional, advanced: per-request placement for the batch number text. Leave this out entirely
    /// for normal use — the fixed <c>PdfProcessing:BatchNumber</c> coordinates from appsettings.json
    /// are used. Only set this if a specific call needs to deviate from that fixed position.
    /// </summary>
    public BatchNumberPositionOverride? BatchNumberPosition { get; set; }

    /// <summary>
    /// Optional, advanced: per-request placement for the signature image. Leave this out entirely
    /// for normal use — the fixed <c>PdfProcessing:Signature</c> coordinates from appsettings.json
    /// are used. Only set this if a specific call needs to deviate from that fixed position.
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
