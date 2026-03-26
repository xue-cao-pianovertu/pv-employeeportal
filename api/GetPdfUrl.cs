using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace PV.AZFunction;

public class GetPdfUrl
{
    private readonly ILogger<GetPdfUrl> _logger;
    public GetPdfUrl(ILogger<GetPdfUrl> logger) => _logger = logger;

    [Function("GetPdfUrl")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var blobName = req.Query["blob"].ToString();
        if (string.IsNullOrWhiteSpace(blobName))
            return new BadRequestObjectResult(new { error = "blob parameter required" });

        var connStr = Environment.GetEnvironmentVariable("AzureStorageConnectionString");
        try
        {
            var serviceClient   = new BlobServiceClient(connStr);
            var containerClient = serviceClient.GetBlobContainerClient("warranty-pdfs");
            var blobClient      = containerClient.GetBlobClient(blobName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "warranty-pdfs",
                BlobName          = blobName,
                Resource          = "b",
                ExpiresOn         = DateTimeOffset.UtcNow.AddMinutes(30),
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return new OkObjectResult(new { url = sasUri.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPdfUrl failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}
