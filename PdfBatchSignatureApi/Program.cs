using System.Reflection;
using PdfBatchSignatureApi.Configuration;
using PdfBatchSignatureApi.Services;
using PdfSharp.Fonts;

var builder = WebApplication.CreateBuilder(args);

// PDFsharp's cross-platform core build has no built-in access to installed system fonts,
// so a font resolver must be registered before any XFont/XGraphics text drawing happens.
GlobalFontSettings.FontResolver = new SystemFontResolver();

builder.Services.Configure<PdfProcessingOptions>(
    builder.Configuration.GetSection(PdfProcessingOptions.SectionName));

builder.Services.AddScoped<IPdfProcessingService, PdfProcessingService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PDF Batch Signature API",
        Version = "v1",
        Description = "Adds a batch number and signature image to an existing PDF supplied by file system path. " +
                      "Intended to run in a trusted, internal application environment — see README for security notes."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configurable CORS: allowed origins are read from configuration ("Cors:AllowedOrigins").
// Development defaults to common localhost frontend ports; production must supply explicit origins.
const string CorsPolicyName = "ConfiguredOrigins";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? (builder.Environment.IsDevelopment()
        ? new[] { "http://localhost:3000", "http://localhost:5173", "http://localhost:4200" }
        : Array.Empty<string>());

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "PDF Batch Signature API v1");
});

app.UseHttpsRedirection();
app.UseCors(CorsPolicyName);
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck")
    .WithTags("Health")
    .Produces(StatusCodes.Status200OK);

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program { }
