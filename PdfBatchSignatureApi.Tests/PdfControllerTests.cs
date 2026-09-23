using System.Net;
using System.Net.Http.Json;
using System.Web;
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

    /// <summary>
    /// Calls the endpoint the same way the described UI does: plain query-string values, no JSON body.
    /// </summary>
    private Task<HttpResponseMessage> CallEndpoint(
        string? pdfPath, string? signatureImagePath, string? batchNumber,
        double? batchNumberX = null, double? batchNumberY = null,
        double? signatureX = null, double? signatureY = null)
    {
        var query = HttpUtility.ParseQueryString(string.Empty);
        if (pdfPath is not null) query["pdfPath"] = pdfPath;
        if (signatureImagePath is not null) query["signatureImagePath"] = signatureImagePath;
        if (batchNumber is not null) query["batchNumber"] = batchNumber;
        if (batchNumberX is not null) query["batchNumberX"] = batchNumberX.Value.ToString();
        if (batchNumberY is not null) query["batchNumberY"] = batchNumberY.Value.ToString();
        if (signatureX is not null) query["signatureX"] = signatureX.Value.ToString();
        if (signatureY is not null) query["signatureY"] = signatureY.Value.ToString();

        return _client.PostAsync($"/api/pdf/add-batch-signature?{query}", content: null);
    }

    [Fact]
    public async Task BareFileNames_AreResolvedAgainstConfiguredAllowedRoots()
    {
        // This is the UI's actual calling pattern: just a file name, not a full path — the API must
        // combine it with PdfProcessing:AllowedPdfRoot / AllowedSignatureRoot itself.
        TestFixtures.CreateSamplePdf(NewPdfPath("Quotation_10025.pdf"));
        TestFixtures.CreateSamplePng(NewSignaturePath("AuthorizedSignature.png"));

        var response = await CallEndpoint("Quotation_10025.pdf", "AuthorizedSignature.png", "BATCH-2026-00125");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddBatchSignatureResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(Path.Combine(_pdfRoot, "Quotation_10025.pdf"), result.InputPdf);
        Assert.Equal(Path.Combine(_signatureRoot, "AuthorizedSignature.png"), result.SignatureImage);
        Assert.True(File.Exists(result.OutputPdf));
    }

    [Fact]
    public async Task BareFileName_TraversalAttempt_ReturnsBadRequest()
    {
        // "..\..\outside.pdf" resolved against AllowedPdfRoot must still be rejected as escaping the root.
        TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(@"..\..\outside.pdf", "sig.png", "B1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

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

        var response = await CallEndpoint(pdfPath, sigPath, "BATCH-2026-00125");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddBatchSignatureResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.True(File.Exists(result.OutputPdf));

        // Original must remain untouched.
        using var originalDoc = PdfReader.Open(pdfPath, PdfDocumentOpenMode.ReadOnly);
        Assert.Equal(1, originalDoc.PageCount);
    }

    [Fact]
    public async Task CoordinateOverrides_AllFourSupplied_ReturnsSuccess()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(
            pdfPath, sigPath, "BATCH-COORD-1",
            batchNumberX: 100, batchNumberY: 100,
            signatureX: 200, signatureY: 300);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AddBatchSignatureResponse>();
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.True(File.Exists(result.OutputPdf));
    }

    [Fact]
    public async Task CoordinateOverrides_PartialSupplied_FallsBackForOmittedAxis()
    {
        // Only batchNumberX supplied; batchNumberY (and the signature coordinates) should fall back
        // to the configured defaults rather than failing or defaulting to zero.
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(pdfPath, sigPath, "BATCH-COORD-2", batchNumberX: 50);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MissingPdfPath_ReturnsBadRequest()
    {
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(null, sigPath, "B1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingSignaturePath_ReturnsBadRequest()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());

        var response = await CallEndpoint(pdfPath, null, "B1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingBatchNumber_ReturnsBadRequest()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(pdfPath, sigPath, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentPdf_ReturnsNotFound()
    {
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(NewPdfPath("does-not-exist.pdf"), sigPath, "B1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NonExistentSignatureImage_ReturnsNotFound()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());

        var response = await CallEndpoint(pdfPath, NewSignaturePath("missing.png"), "B1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidPdf_ReturnsUnprocessableEntity()
    {
        var pdfPath = TestFixtures.CreateInvalidPdf(NewPdfPath("bad.pdf"));
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(pdfPath, sigPath, "B1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task InvalidImage_ReturnsUnprocessableEntity()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(NewPdfPath());
        var sigPath = TestFixtures.CreateInvalidImage(NewSignaturePath("bad.png"));

        var response = await CallEndpoint(pdfPath, sigPath, "B1");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PathOutsideAllowedRoot_ReturnsBadRequest()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "OutsideAllowedRoot_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideDir);
        var pdfPath = TestFixtures.CreateSamplePdf(Path.Combine(outsideDir, "outside.pdf"));
        var sigPath = TestFixtures.CreateSamplePng(NewSignaturePath());

        var response = await CallEndpoint(pdfPath, sigPath, "B1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Directory.Delete(outsideDir, recursive: true);
    }
}
