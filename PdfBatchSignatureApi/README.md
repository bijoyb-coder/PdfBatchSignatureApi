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
    "OutputFolder": "D:\\OCR_Edit\\PdfBatchSignatureApi\\Output",
    "AllowedPdfRoot": "D:\\OCR_Edit\\PdfBatchSignatureApi\\PdfSource",
    "AllowedSignatureRoot": "D:\\OCR_Edit\\PdfBatchSignatureApi\\Signatures",
    "BatchNumber": {
      "PageNumber": 1,
      "X": 605,
      "Y": 259,
      "FontSize": 10,
      "FontName": "Arial",
      "Width": 150,
      "Height": 14
    },
    "Signature": {
      "PageNumber": 1,
      "X": 150,
      "Y": 482,
      "Width": 200,
      "Height": 42
    }
  },
  "Cors": {
    "AllowedOrigins": [ "http://localhost:3000" ]
  }
}
```

All values are bound to strongly typed classes in [`Configuration/PdfProcessingOptions.cs`](Configuration/PdfProcessingOptions.cs)
via `IOptions<PdfProcessingOptions>` — nothing is hard-coded in `PdfProcessingService`.

> **Note on the defaults above:** these were measured directly against the sample IFB Industries test
> certificate template (792×612pt landscape) used during development — `BatchNumber` targets the blank
> cell beside "TEST RESULTS", and `Signature` targets the clear blank space to the *left* of that
> template's pre-existing company stamp, with a comfortable gap so the new signature and the existing
> stamp both stay fully legible and never touch. **If your real PDFs use a different layout, or don't
> already contain a stamp in that spot, re-measure and adjust `Signature.X`/`Width`/`Height` as
> needed** — see [Coordinate System Explanation](#12-coordinate-system-explanation).

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

This endpoint takes **plain query-string parameters — no JSON body.** This matches the intended calling
pattern directly: a front-end has one text box for the PDF file name, one text box for the signature
file name, and one input for the batch number, and the system calls this endpoint passing those three
values as-is. The API resolves each file name against the server's configured
`PdfProcessing:AllowedPdfRoot` / `AllowedSignatureRoot` folders, and always stamps using the fixed
`PdfProcessing:BatchNumber` / `Signature` X/Y coordinates from `appsettings.json` — no positioning
values are ever sent by the caller.

```
POST /api/pdf/add-batch-signature?pdfPath=Quotation_10025.pdf&signatureImagePath=AuthorizedSignature.png&batchNumber=BATCH-2026-00125
```

Equivalent curl:

```bash
curl -X POST "http://localhost:5080/api/pdf/add-batch-signature?pdfPath=Quotation_10025.pdf&signatureImagePath=AuthorizedSignature.png&batchNumber=BATCH-2026-00125"
```

Given `"PdfProcessing:AllowedPdfRoot": "D:\\RajendraGlass\\Documents\\Quotation"` and
`"AllowedSignatureRoot": "D:\\RajendraGlass\\Signatures"`, this resolves to
`D:\RajendraGlass\Documents\Quotation\Quotation_10025.pdf` and
`D:\RajendraGlass\Signatures\AuthorizedSignature.png` respectively — the caller never needs to know or
send the full path. A full path is still accepted too (see [Security Considerations](#13-security-considerations-for-file-paths)
for how both are validated), but the normal case only ever needs bare file names.

In Swagger, click **Try it out** on `POST /api/pdf/add-batch-signature` and three plain text boxes
appear under **Parameters** — `pdfPath`, `signatureImagePath`, `batchNumber` — fill each in directly
and click **Execute**. No JSON to write.

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
| 400 | Missing/invalid `pdfPath`, `signatureImagePath`, or `batchNumber`; path outside allowed root; wrong extension |
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
- `Width`, `Height` — a **maximum bounding box** in points, not a forced size. The signature image's
  own aspect ratio is always preserved — it is scaled uniformly to fit inside `Width` x `Height` without
  stretching or squashing, anchored to the box's top-left corner at `(X, Y)`. A tall/narrow signature
  and a short/wide one dropped into the same box will end up different actual sizes, but neither will
  ever look distorted.

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

This API accepts **file names or file system paths** as plain query-string values — it does not use
file uploads. Because of this:

- **`pdfPath`/`signatureImagePath` resolution**: a bare file name with no drive/root (e.g.
  `"Quotation_10025.pdf"` — what the UI described in [§8](#8-example-api-request) sends) is resolved
  directly under `PdfProcessing:AllowedPdfRoot` / `AllowedSignatureRoot`. A full/rooted path
  (`"D:\\...\\Quotation_10025.pdf"`) is accepted too, but is still required to resolve inside that same
  allowed root — a caller can never point outside it either way.
- **`AllowedPdfRoot`** and **`AllowedSignatureRoot`** in `appsettings.json` restrict `pdfPath` and
  `signatureImagePath` to files located within those directory trees. Any resolved path (via
  `Path.GetFullPath`, which also normalizes `..` traversal) that falls outside the configured root is
  rejected with `400 Bad Request` — this blocks path traversal attempts, whether spelled out as an
  absolute path (`..\..\Windows\System32\...`) or hidden inside what looks like a bare file name
  (`..\..\Windows\System32\config.pdf`).
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
├── Controllers/PdfController.cs           HTTP layer: [FromQuery] parameters, validates ModelState, maps exceptions to status codes
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

If you want to test manually against your own files, just fill in the `pdfPath` / `signatureImagePath`
text boxes in Swagger with any real `.pdf` and `.png`/`.jpg` file name that exists under your configured
`AllowedPdfRoot` / `AllowedSignatureRoot`.

## Quick Reference: Where to Change What

Every coordinate is fixed in `appsettings.json` — the request only ever carries `pdfPath`,
`signatureImagePath`, and `batchNumber` as plain query-string values; there is no way to override
position from a request.

| Setting | Key |
|---|---|
| Batch Number X/Y | `appsettings.json`: `PdfProcessing:BatchNumber:X` / `:Y` |
| Batch Number font size | `appsettings.json`: `PdfProcessing:BatchNumber:FontSize` |
| Batch Number font | `appsettings.json`: `PdfProcessing:BatchNumber:FontName` (also see `SystemFontResolver.FamilyMap` to add new fonts) |
| Signature X/Y | `appsettings.json`: `PdfProcessing:Signature:X` / `:Y` |
| Signature width | `appsettings.json`: `PdfProcessing:Signature:Width` |
| Signature height | `appsettings.json`: `PdfProcessing:Signature:Height` |
| Target page number | `appsettings.json`: `PdfProcessing:BatchNumber:PageNumber` / `PdfProcessing:Signature:PageNumber` (independent) |
| Input/output allowed folders | `appsettings.json`: `PdfProcessing:AllowedPdfRoot`, `PdfProcessing:AllowedSignatureRoot`, `PdfProcessing:OutputFolder` |
