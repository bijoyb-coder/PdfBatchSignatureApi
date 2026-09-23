namespace PdfBatchSignatureApi.Configuration;

/// <summary>
/// Strongly typed configuration bound from the "PdfProcessing" section of appsettings.json.
/// Controls output location, allowed root folders for path validation, and default
/// positioning for the batch number text and signature image overlays.
/// </summary>
public class PdfProcessingOptions
{
    public const string SectionName = "PdfProcessing";

    /// <summary>Folder where generated (modified) PDFs are written. Created automatically if missing.</summary>
    public string OutputFolder { get; set; } = string.Empty;

    /// <summary>Root folder that all supplied PdfPath values must resolve within.</summary>
    public string AllowedPdfRoot { get; set; } = string.Empty;

    /// <summary>Root folder that all supplied SignatureImagePath values must resolve within.</summary>
    public string AllowedSignatureRoot { get; set; } = string.Empty;

    /// <summary>Default position/size/font settings for the batch number text overlay.</summary>
    public BatchNumberSettings BatchNumber { get; set; } = new();

    /// <summary>Default position/size settings for the signature image overlay.</summary>
    public SignatureSettings Signature { get; set; } = new();
}

/// <summary>
/// Positioning and font configuration for the Batch Number text overlay.
/// Coordinates are in points, measured from the TOP-LEFT corner of the page
/// (this API converts to PDFsharp's native top-left-origin XGraphics space automatically).
/// </summary>
public class BatchNumberSettings
{
    /// <summary>1-based page number the batch number is drawn on.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>X coordinate in points from the left edge of the page.</summary>
    public double X { get; set; } = 450;

    /// <summary>Y coordinate in points from the top edge of the page.</summary>
    public double Y { get; set; } = 720;

    /// <summary>Font size in points.</summary>
    public double FontSize { get; set; } = 12;

    /// <summary>Font family name. Resolved via the app's font resolver; see README for supported fonts.</summary>
    public string FontName { get; set; } = "Arial";

    /// <summary>
    /// Text color as a hex string: "#RRGGBB" or "#AARRGGBB" (e.g. "#FF0000" for red, "#000000" for
    /// black). Parsed via <see cref="System.Drawing.ColorTranslator"/>-style hex parsing in
    /// <c>PdfProcessingService</c>. Defaults to red.
    /// </summary>
    public string FontColor { get; set; } = "#FF0000";

    /// <summary>Optional bounding box width in points, used for text alignment/wrapping.</summary>
    public double? Width { get; set; }

    /// <summary>Optional bounding box height in points.</summary>
    public double? Height { get; set; }
}

/// <summary>
/// Positioning and size configuration for the signature image overlay.
/// Coordinates are in points, measured from the TOP-LEFT corner of the page.
/// </summary>
public class SignatureSettings
{
    /// <summary>1-based page number the signature is drawn on.</summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>X coordinate in points from the left edge of the page.</summary>
    public double X { get; set; } = 450;

    /// <summary>Y coordinate in points from the top edge of the page.</summary>
    public double Y { get; set; } = 650;

    /// <summary>Width of the drawn signature image in points.</summary>
    public double Width { get; set; } = 120;

    /// <summary>Height of the drawn signature image in points.</summary>
    public double Height { get; set; } = 50;
}
