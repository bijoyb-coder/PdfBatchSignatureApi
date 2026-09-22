using PdfSharp.Fonts;

namespace PdfBatchSignatureApi.Services;

/// <summary>
/// PDFsharp 6.x (the cross-platform "core" build used by this project) does not ship with GDI/WPF
/// platform font access, so it cannot read installed system fonts unless a <see cref="IFontResolver"/>
/// is registered explicitly (see PDFsharp docs: "Font Resolving"). This resolver loads well-known
/// TrueType font files directly from disk (Windows fonts folder, with common Linux fallbacks), and
/// falls back to a bundled font if the requested family cannot be found.
/// </summary>
public class SystemFontResolver : IFontResolver
{
    private static readonly string[] WindowsFontDirs = { @"C:\Windows\Fonts" };
    private static readonly string[] LinuxFontDirs =
    {
        "/usr/share/fonts/truetype/dejavu",
        "/usr/share/fonts/truetype/liberation",
        "/usr/share/fonts/truetype/freefont"
    };

    // Maps a requested family name (case-insensitive) to candidate font file names, in priority order.
    private static readonly Dictionary<string, (string Regular, string Bold, string Italic, string BoldItalic)> FamilyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Arial"] = ("arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"),
        ["Helvetica"] = ("arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"),
        ["Times New Roman"] = ("times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf"),
        ["Courier New"] = ("cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf"),
        ["DejaVu Sans"] = ("DejaVuSans.ttf", "DejaVuSans-Bold.ttf", "DejaVuSans-Oblique.ttf", "DejaVuSans-BoldOblique.ttf"),
    };

    private const string FallbackFamilyName = "PdfBatchSignatureApi#Fallback";

    public string DefaultFontName => "Arial";

    public byte[] GetFont(string faceName)
    {
        var path = ResolveFacePath(faceName);
        if (path != null && File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        // Last-resort fallback: first usable font file we can find on the system.
        var anyFont = FindAnyAvailableFont();
        if (anyFont != null)
        {
            return File.ReadAllBytes(anyFont);
        }

        throw new PdfProcessingException(
            PdfProcessingErrorType.Unprocessable,
            $"No usable TrueType font could be located on the server for face '{faceName}'. " +
            "Install a standard font (e.g. Arial/DejaVu Sans) or configure a different FontName.");
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Normalize unknown families to the fallback so GetFont always has a deterministic face name.
        var resolvedFamily = FamilyMap.ContainsKey(familyName) ? familyName : FallbackFamilyName;
        var faceName = BuildFaceName(resolvedFamily, isBold, isItalic);
        return new FontResolverInfo(faceName);
    }

    private static string BuildFaceName(string family, bool bold, bool italic)
    {
        return $"{family}#{(bold ? "B" : "")}{(italic ? "I" : "")}";
    }

    private string? ResolveFacePath(string faceName)
    {
        var parts = faceName.Split('#', 2);
        var family = parts[0];
        var style = parts.Length > 1 ? parts[1] : string.Empty;
        var bold = style.Contains('B');
        var italic = style.Contains('I');

        if (!FamilyMap.TryGetValue(family, out var files))
        {
            // Fallback family: prefer DejaVu Sans (common on Linux), else Arial (Windows).
            files = FamilyMap["DejaVu Sans"];
            var dejaVuPath = FindInDirs(bold && italic ? files.BoldItalic : bold ? files.Bold : italic ? files.Italic : files.Regular);
            if (dejaVuPath != null) return dejaVuPath;
            files = FamilyMap["Arial"];
        }

        var fileName = bold && italic ? files.BoldItalic : bold ? files.Bold : italic ? files.Italic : files.Regular;
        return FindInDirs(fileName);
    }

    private static string? FindInDirs(string fileName)
    {
        foreach (var dir in WindowsFontDirs.Concat(LinuxFontDirs))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindAnyAvailableFont()
    {
        foreach (var dir in WindowsFontDirs.Concat(LinuxFontDirs))
        {
            if (!Directory.Exists(dir)) continue;
            var ttf = Directory.EnumerateFiles(dir, "*.ttf").FirstOrDefault();
            if (ttf != null) return ttf;
        }
        return null;
    }
}
