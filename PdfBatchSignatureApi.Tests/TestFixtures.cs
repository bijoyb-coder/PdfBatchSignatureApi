using PdfBatchSignatureApi.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace PdfBatchSignatureApi.Tests;

internal static class TestFixtures
{
    static TestFixtures()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new SystemFontResolver();
        }
    }

    public static string CreateSamplePdf(string path, int pageCount = 1)
    {
        using var document = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            var page = document.AddPage();
            using var gfx = XGraphics.FromPdfPage(page);
            gfx.DrawString($"Sample page {i + 1}", new XFont("Arial", 14, XFontStyleEx.Regular), XBrushes.Black, new XPoint(50, 50));
        }
        document.Save(path);
        return path;
    }

    public static string CreateInvalidPdf(string path)
    {
        File.WriteAllText(path, "This is not a real PDF file.");
        return path;
    }

    public static string CreateSamplePng(string path)
    {
        using var bitmap = new System.Drawing.Bitmap(40, 20);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.White);
            g.DrawLine(System.Drawing.Pens.Black, 0, 0, 40, 20);
        }
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        return path;
    }

    public static string CreateInvalidImage(string path)
    {
        File.WriteAllText(path, "not a real image");
        return path;
    }
}
