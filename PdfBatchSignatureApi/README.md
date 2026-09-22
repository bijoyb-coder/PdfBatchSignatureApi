# PDF Batch Signature API

## 1. Project Overview

An ASP.NET Core Web API (.NET 10) that opens an **existing** PDF from a file system path, overlays a
Batch Number text string and a signature image onto a configured page, and saves the result as a
**new** PDF file. The original PDF is never modified. Built with [PDFsharp](http://www.pdfsharp.net/) 6.2.4 —
no Adobe, Azure, or other paid PDF services are used.

The API is designed to run inside a **trusted internal environment** (e.g. alongside an ERP/document
system on the same server or LAN) because it accepts raw file system paths rather than uploaded files.
See [Security Considerations](#13-security-considerations-for-file-paths) before exposing it publicly.

## 2. Requirements

- .NET 10 SDK
- Windows, Linux, or macOS (the service uses PDFsharp's cross-platform "core" build)
- Read/write access to the PDF, signature image, and output folders

## 3. Installation

```bash
cd PdfBatchSignatureApi
dotnet restore
dotnet build
```

## 4. NuGet Packages

| Package | Version | Purpose |
|---|---|---|
| `PDFsharp` | 6.2.4 | Opens, modifies, and saves PDFs (cross-platform core build — not GDI/WPF) |
| `Swashbuckle.AspNetCore` | 7.2.0 | Swagger / OpenAPI generation and UI |

Test project additionally uses `Microsoft.AspNetCore.Mvc.Testing`, `xunit`, and `System.Drawing.Common`
(test-fixture image generation only — not a runtime dependency of the API).

## 5. Configuring appsettings.json

```json
{
  "PdfProcessing": {
    "OutputFolder": "D:\\RajendraGlass\\Documents\\Processed",
    "AllowedPdfRoot": "D:\\RajendraGlass\\Documents",
    "AllowedSignatureRoot": "D:\\RajendraGlass\\Signatures",
    "BatchNumber": {
      "PageNumber": 1,
      "X": 450,
      "Y": 720,
      "FontSize": 12,
      "FontName": "Arial"
    },
    "Signature": {
      "PageNumber": 1,
      "X": 450,
      "Y": 650,
      "Width": 120,
      "Height": 50
    }
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]
  }
}
```

All values are bound to strongly typed classes in [`Configuration/PdfProcessingOptions.cs`](Configuration/PdfProcessingOptions.cs)
via `IOptions<PdfProcessingOptions>` — nothing is hard-coded in `PdfProcessingService`.

## 6. Starting the API

```bash
dotnet run
```

By default (`Properties/launchSettings.json`) the API listens on `http://localhost:5080` (and
`https://localhost:5081` under the `https` launch profile) and opens Swagger automatically.

## 7. Swagger URL

```
http://localhost:5080/swagger
```

## 8. Example API Request

`POST /api/pdf/add-batch-signature`

```json
{
  "pdfPath": "D:\\RajendraGlass\\Documents\\Quotation\\Quotation_10025.pdf",
  "signatureImagePath": "D:\\RajendraGlass\\Signatures\\AuthorizedSignature.png",
  "batchNumber": "BATCH-2026-00125"
}
```

Use Swagger's **Try it out** button on `POST /api/pdf/add-batch-signature` to submit this directly.

## 9. Example Response

Success (`200 OK`):

```json
{
  "success": true,
  "message": "PDF processed successfully.",
  "batchNumber": "BATCH-2026-00125",
  "inputPdf": "D:\\RajendraGlass\\Documents\\Quotation\\Quotation_10025.pdf",
  "signatureImage": "D:\\RajendraGlass\\Signatures\\AuthorizedSignature.png",
  "outputPdf": "D:\\RajendraGlass\\Documents\\Processed\\Quotation_10025_Batch_BATCH-2026-00125_Signed.pdf"
}
```

Failure (e.g. `404 Not Found`):

```json
{
  "success": false,
  "message": "PDF file was not found: D:\\RajendraGlass\\Documents\\Quotation\\Missing.pdf",
  "errors": null
}
```

| Status | Meaning |
|---|---|
| 200 | PDF processed successfully |
| 400 | Missing/invalid `PdfPath`, `SignatureImagePath`, or `BatchNumber`; path outside allowed root; wrong extension |
| 404 | PDF or signature image file does not exist |
| 422 | File exists but is corrupt/unreadable, image format unsupported, or `PageNumber` out of range |
| 500 | Unexpected server error (full details logged server-side only, never returned to the client) |

## 10. Configuring Batch Number Position

Edit `PdfProcessing:BatchNumber` in `appsettings.json` (or `appsettings.Development.json`):

- `PageNumber` — 1-based page the batch number is drawn on.
- `X`, `Y` — position in points (see [Coordinate System](#12-coordinate-system-explanation)).
- `FontSize` — point size.
- `FontName` — see [Font Behavior](#font-behavior) below.
- `Width` / `Height` *(optional)* — if both are set, the text is drawn inside that bounding box
  (top-left aligned); if omitted, the text is drawn starting at `(X, Y)` with no wrapping.

Code: [`Configuration/PdfProcessingOptions.cs`](Configuration/PdfProcessingOptions.cs) (`BatchNumberSettings`),
consumed in [`Services/PdfProcessingService.cs`](Services/PdfProcessingService.cs) (`DrawBatchNumber`).

## 11. Configuring Signature Position

Edit `PdfProcessing:Signature` in `appsettings.json`:

- `PageNumber` — 1-based page the signature image is drawn on.
- `X`, `Y` — top-left position in points.
- `Width`, `Height` — size in points the image is scaled to.

Code: `SignatureSettings` in [`Configuration/PdfProcessingOptions.cs`](Configuration/PdfProcessingOptions.cs),
consumed in `DrawSignature` in [`Services/PdfProcessingService.cs`](Services/PdfProcessingService.cs).

## 12. Coordinate System Explanation

PDFsharp's native PDF coordinate space has its origin `(0,0)` at the **bottom-left** of the page with Y
increasing **upward**. However, `XGraphics.FromPdfPage(page)` — which this service uses exclusively —
creates a graphics context in **top-left origin, Y-down** space by default (the same convention as
screen/GDI coordinates). **This API's `X`/`Y` values are therefore interpreted as offsets from the
top-left corner of the page, with Y increasing downward**, exactly as configured in `appsettings.json`.

For a standard US Letter page (612 x 792 points):
- `X: 450, Y: 720` places text near the bottom-right area of the page (720pt down from the top, on a
  792pt-tall page, i.e. 72pt / 1 inch from the bottom).
- `X: 0, Y: 0` is the top-left corner.

No manual flipping of Y is performed or required — this is `XGraphics`' default behavior for a page.

### Font Behavior

PDFsharp 6.x's cross-platform core build (used here, not the GDI/WPF variants) has **no built-in access
to installed system fonts** — it requires an explicit `PdfSharp.Fonts.IFontResolver`. This project
registers [`Services/SystemFontResolver.cs`](Services/SystemFontResolver.cs) at startup
(`GlobalFontSettings.FontResolver`), which:

1. Maps `FontName` (`Arial`, `Helvetica`, `Times New Roman`, `Courier New`, `DejaVu Sans`) to known
   TrueType font files.
2. Looks for those files in `C:\Windows\Fonts` (Windows) or common Linux font directories
   (`/usr/share/fonts/truetype/dejavu`, `/liberation`, `/freefont`).
3. Falls back to `DejaVu Sans` / `Arial`, then to the first `.ttf` file found on the system, if the
   requested family or file isn't present.
4. Throws a clear `422 Unprocessable Entity` error if no usable font file exists at all.

**Do not assume a custom/branded font is installed on the deployment server.** Stick to the family
names above, or extend `SystemFontResolver.FamilyMap` with your own font file mappings.

## 13. Security Considerations for File Paths

This API accepts **arbitrary file system paths** in the request body — it does not use file uploads.
Because of this:

- **`AllowedPdfRoot`** and **`AllowedSignatureRoot`** in `appsettings.json` restrict `PdfPath` and
  `SignatureImagePath` to files located within those directory trees. Any path that resolves (via
  `Path.GetFullPath`, which also normalizes `..` traversal) outside the configured root is rejected
  with `400 Bad Request` — this blocks path traversal attempts (e.g. `..\..\Windows\System32\...`).
- Extensions are strictly checked (`.pdf` for the PDF; `.png`/`.jpg`/`.jpeg` for the signature) before
  any file I/O occurs.
- If `AllowedPdfRoot` / `AllowedSignatureRoot` are left blank, path restriction is disabled — **only do
  this in a fully trusted, single-tenant, network-isolated environment.**
- **This API must not be exposed directly to untrusted networks or the public internet.** It grants
  callers read access to any file under the allowed roots and write access to the output folder. Run it
  behind your organization's existing authentication/authorization layer (reverse proxy, VPN, internal
  network only) if any external exposure is required — this project intentionally does not add its own
  authentication, per the stated requirements.
- No file content (PDF/image bytes) is ever logged — only paths, the batch number, and processing
  metadata (see `PdfProcessingService` logging calls).

## 14. Production Deployment Notes

- Set `PdfProcessing:AllowedPdfRoot` / `AllowedSignatureRoot` / `OutputFolder` to real, restrictive
  paths in `appsettings.Production.json` (create this file) or environment-variable overrides — never
  ship with a wide-open root.
- Set `Cors:AllowedOrigins` explicitly for production; the default when unset is **no allowed origins**
  (CORS requests are rejected) rather than `AllowAnyOrigin`, which this project intentionally never
  configures.
- Run behind HTTPS termination (reverse proxy / IIS / Kestrel with a certificate); `app.UseHttpsRedirection()`
  is already enabled.
- Consider restricting Swagger UI (`/swagger`) to internal networks only, or disabling it in production,
  since it fully documents and exercises a file-system-path-accepting endpoint.
- The service creates the output directory automatically (`Directory.CreateDirectory`) but does **not**
  create `AllowedPdfRoot` / `AllowedSignatureRoot` — those must already exist and contain your source files.
- Concurrency: the service holds no mutable static state and reads/writes only the specific files named
  in each request; output filenames are de-duplicated automatically if a collision is detected (see
  `BuildOutputPath` in `PdfProcessingService`), so concurrent requests are safe.

## Architecture

```
PdfBatchSignatureApi/
├── Controllers/PdfController.cs           HTTP layer: validates ModelState, maps exceptions to status codes
├── Models/AddBatchSignatureRequest.cs     Request DTO with DataAnnotations validation
├── Models/AddBatchSignatureResponse.cs    Response DTOs (success + error)
├── Services/IPdfProcessingService.cs      Service contract
├── Services/PdfProcessingService.cs       PDFsharp logic: validation, drawing, saving
├── Services/PdfProcessingException.cs     Typed exception -> HTTP status mapping
├── Services/SystemFontResolver.cs         IFontResolver implementation for PDFsharp
├── Configuration/PdfProcessingOptions.cs  Strongly typed appsettings.json bindings
├── Program.cs                             DI, Swagger, CORS, font resolver registration, /health
├── appsettings.json / appsettings.Development.json
└── PdfBatchSignatureApi.csproj

PdfBatchSignatureApi.Tests/
├── TestFixtures.cs            Generates real sample PDFs/PNGs at test time (no binary assets checked in)
├── PdfControllerTests.cs      Integration tests via WebApplicationFactory<Program>
└── InvalidPageNumberTests.cs  Out-of-range PageNumber scenario (separate app config)
```

## Testing

```bash
cd PdfBatchSignatureApi.Tests
dotnet test
```

Tests use `WebApplicationFactory<Program>` with an in-memory configuration override pointing
`AllowedPdfRoot` / `AllowedSignatureRoot` / `OutputFolder` at a temp directory per test class, so no
sample files need to be checked into source control — `TestFixtures.cs` generates a real single-page
PDF (via PDFsharp) and a real PNG (via `System.Drawing`, test-only dependency) on demand. Covered
scenarios: valid request end-to-end (and original-file-unchanged check), missing PDF/signature/batch
number, non-existent PDF/image, invalid/corrupt PDF, invalid/corrupt image, path outside the allowed
root, out-of-range page number, output file creation, and the `/health` endpoint.

If you want to test manually against your own files, just point `PdfPath` / `SignatureImagePath` in the
Swagger request at any real `.pdf` and `.png`/`.jpg` on disk that fall under your configured
`AllowedPdfRoot` / `AllowedSignatureRoot`.

## Quick Reference: Where to Change What

| Setting | File | Key |
|---|---|---|
| Batch Number X/Y | `appsettings.json` | `PdfProcessing:BatchNumber:X` / `:Y` |
| Batch Number font size | `appsettings.json` | `PdfProcessing:BatchNumber:FontSize` |
| Batch Number font | `appsettings.json` | `PdfProcessing:BatchNumber:FontName` (also see `SystemFontResolver.FamilyMap` to add new fonts) |
| Signature X/Y | `appsettings.json` | `PdfProcessing:Signature:X` / `:Y` |
| Signature width | `appsettings.json` | `PdfProcessing:Signature:Width` |
| Signature height | `appsettings.json` | `PdfProcessing:Signature:Height` |
| Target page number | `appsettings.json` | `PdfProcessing:BatchNumber:PageNumber` and `PdfProcessing:Signature:PageNumber` (independent) |
| Input/output allowed folders | `appsettings.json` | `PdfProcessing:AllowedPdfRoot`, `PdfProcessing:AllowedSignatureRoot`, `PdfProcessing:OutputFolder` |
