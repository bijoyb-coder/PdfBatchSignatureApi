using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PdfBatchSignatureApi.Models;
using PdfSharp.Pdf.IO;
using Xunit;

namespace PdfBatchSignatureApi.Tests;

public class PdfControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _tempRoot;
    private readonly string _pdfRoot;
    private readonly string _signatureRoot;
    private readonly string _outputRoot;

    public PdfControllerTests(WebApplicationFactory<Program> baseFactory)
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PdfBatchSignatureApiTests_" + Guid.NewGuid().ToString("N"));
        _pdfRoot = Path.Combine(_tempRoot, "Pdfs");
        _signatureRoot = Path.Combine(_tempRoot, "Signatures");
        _outputRoot = Path.Combine(_tempRoot, "Processed");
        Directory.CreateDirectory(_pdfRoot);
        Directory.CreateDirectory(_signatureRoot);

        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PdfProcessing:OutputFolder"] = _outputRoot,
                    ["PdfProcessing:AllowedPdfRoot"] = _pdfRoot,
                    ["PdfProcessing:AllowedSignatureRoot"] = _signatureRoot,
                    ["PdfProcessing:BatchNumber:PageNumber"] = "1",
                    ["PdfProcessing:Signature:PageNumber"] = "1"
                });
            });
        });

        _client = factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best effort cleanup */ }
        }
    }

    private string NewPdfPath(string name = "input.pdf") => Path.Combine(_pdfRoot, name);
    private string NewSignaturePath(string name = "sig.png") => Path.Combine(_signatureRoot, name);

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("Healthy", body!["status"]);
    }

    [Fact]
    public async Task ValidRequest_ReturnsSuccessAndCreatesOutputFile()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var request = new AddBatchSignatureRequest
        {
            PdfPath = pdfPath,
            SignatureImagePath = sigPath,
            BatchNumber = "BATCH-2026-00125"
        };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddBatchSignatureResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.True(File.Exists(result.OutputPdf));

        // Original must remain untouched.
        var originalBytesAfter = await File.ReadAllBytesAsync(pdfPath);
        using var originalDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(1, originalDoc.PageCount);
    }

    [Fact]
    public async Task PerRequestPositionOverride_UsesRequestCoordinatesInsteadOfConfigDefaults()
    {
        // Two-page PDF so the override's PageNumber (2) would be invalid under the config default (1),
        // proving the request's values were actually used rather than silently falling back.
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath(), pageCount: 2);
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var request = new AddBatchSignatureRequest
        {
            PdfPath = pdfPath,
            SignatureImagePath = sigPath,
            BatchNumber = "BATCH-OVERRIDE-1",
            BatchNumberPosition = new BatchNumberPositionOverride { PageNumber = 2, X = 50, Y = 60, FontSize = 9 },
            SignaturePosition = new SignaturePositionOverride { PageNumber = 2, X = 100, Y = 200, Width = 80, Height = 30 }
        };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddBatchSignatureResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.True(File.Exists(result.OutputPdf));
    }

    [Fact]
    public async Task PerRequestPositionOverride_PartialFields_FallBackToConfigDefaultsForOmittedFields()
    {
        // Only X/Y overridden; PageNumber/FontSize/etc. should fall back to the config defaults (page 1)
        // rather than the override object's C# default values (e.g. PageNumber 0, which would be invalid).
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath(), pageCount: 1);
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var request = new AddBatchSignatureRequest
        {
            PdfPath = pdfPath,
            SignatureImagePath = sigPath,
            BatchNumber = "BATCH-PARTIAL-1",
            BatchNumberPosition = new BatchNumberPositionOverride { X = 75, Y = 85 },
            SignaturePosition = new SignaturePositionOverride { X = 100, Y = 100 }
        };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PerRequestPositionOverride_PageNumberBeyondDocument_ReturnsUnprocessableEntity()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath(), pageCount: 1);
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var request = new AddBatchSignatureRequest
        {
            PdfPath = pdfPath,
            SignatureImagePath = sigPath,
            BatchNumber = "BATCH-BADPAGE-1",
            BatchNumberPosition = new BatchNumberPositionOverride { PageNumber = 9 }
        };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task MissingPdfPath_ReturnsBadRequest()
    {
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());
        var request = new AddBatchSignatureRequest { PdfPath = "", SignatureImagePath = sigPath, BatchNumber = "B1" };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingSignaturePath_ReturnsBadRequest()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var request = new AddBatchSignatureRequest { PdfPath = pdfPath, SignatureImagePath = "", BatchNumber = "B1" };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingBatchNumber_ReturnsBadRequest()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());
        var request = new AddBatchSignatureRequest { PdfPath = pdfPath, SignatureImagePath = sigPath, BatchNumber = "" };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentPdf_ReturnsNotFound()
    {
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());
        var request = new AddBatchSignatureRequest
        {
            PdfPath = NewPdfPath("does-not-exist.pdf"),
            SignatureImagePath = sigPath,
            BatchNumber = "B1"
        };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentSignatureImage_ReturnsNotFound()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var request = new AddBatchSignatureRequest
        {
            PdfPath = pdfPath,
            SignatureImagePath = NewSignaturePath("missing.png"),
            BatchNumber = "B1"
        };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPdf_ReturnsUnprocessableEntity()
    {
        var pdfPath = TestFixtures.CreateInvalidPdf(NewPdfPath("bad.pdf"));
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());
        var request = new AddBatchSignatureRequest { PdfPath = pdfPath, SignatureImagePath = sigPath, BatchNumber = "B1" };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task InvalidImage_ReturnsUnprocessableEntity()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var sigPath = TestFixtures.CreateInvalidImage(NewSignaturePath("bad.png"));
        var request = new AddBatchSignatureRequest { PdfPath = pdfPath, SignatureImagePath = sigPath, BatchNumber = "B1" };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PathOutsideAllowedRoot_ReturnsBadRequest()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "OutsideAllowedRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var pdfPath = TestFixtures.CreateSamplePdf(Path.Combine(outsideDir, "outside.pdf"));
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var request = new AddBatchSignatureRequest { PdfPath = pdfPath, SignatureImagePath = sigPath, BatchNumber = "B1" };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Directory.Delete(outsideDir, recursive: true);
    }

}
