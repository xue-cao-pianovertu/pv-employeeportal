using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace PV.AZFunction;

public class GetApvPolicyUrl
{
    private readonly ILogger<GetApvPolicyUrl> _logger;
    public GetApvPolicyUrl(ILogger<GetApvPolicyUrl> logger) => _logger = logger;

    [Function("GetApvPolicyUrl")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var lang = req.Query["lang"].ToString().ToLowerInvariant();
        var blobName = lang switch
        {
            "en" => "apv-policy-en.pdf",
            "zh" => "apv-policy-zh.pdf",
            _    => "apv-policy-fr.pdf",
        };

        var connStr = Environment.GetEnvironmentVariable("AzureStorageConnectionString");
        try
        {
            var serviceClient   = new BlobServiceClient(connStr);
            var containerClient = serviceClient.GetBlobContainerClient("apv-docs");
            var blobClient      = containerClient.GetBlobClient(blobName);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = "apv-docs",
                BlobName          = blobName,
                Resource          = "b",
                ExpiresOn         = DateTimeOffset.UtcNow.AddHours(24),
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return new OkObjectResult(new { url = sasUri.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetApvPolicyUrl failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}
