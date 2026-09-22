using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using PdfBatchSignatureApi.Models;
using Xunit;

namespace PdfBatchSignatureApi.Tests;

/// <summary>Verifies requests fail cleanly when the configured page number exceeds the document's page count.</summary>
public class InvalidPageNumberTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _tempRoot;
    private readonly string _pdfRoot;
    private readonly string _signatureRoot;

    public InvalidPageNumberTests(WebApplicationFactory<Program> baseFactory)
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PdfBatchSignatureApiTests_" + Guid.NewGuid().ToString("N"));
        _pdfRoot = Path.Combine(_tempRoot, "Pdfs");
        _signatureRoot = Path.Combine(_tempRoot, "Signatures");
        var outputRoot = Path.Combine(_tempRoot, "Processed");
        Directory.CreateDirectory(_pdfRoot);
        Directory.CreateDirectory(_signatureRoot);

        var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PdfProcessing:OutputFolder"] = outputRoot,
                    ["PdfProcessing:AllowedPdfRoot"] = _pdfRoot,
                    ["PdfProcessing:AllowedSignatureRoot"] = _signatureRoot,
                    ["PdfProcessing:BatchNumber:PageNumber"] = "5",
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

    [Fact]
    public async Task BatchNumberPageNumberBeyondDocumentPageCount_ReturnsUnprocessableEntity()
    {
        var pdfPath = TestFixtures.CreateSamplePdf(Path.Combine(_pdfRoot, "input.pdf"), pageCount: 1);
        var sigPath = TestFixtures.CreateSamplePng(Path.Combine(_signatureRoot, "sig.png"));

        var request = new AddBatchSignatureRequest { PdfPath = pdfPath, SignatureImagePath = sigPath, BatchNumber = "B1" };

        var response = await _client.PostAsJsonAsync("/api/pdf/add-batch-signature", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
