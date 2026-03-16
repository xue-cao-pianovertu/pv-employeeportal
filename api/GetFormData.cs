using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Net;
using System.Text.Json;

namespace PV.AZFunction
{
    public class GetFormData
    {
        private readonly ILogger<GetFormData> _logger;

        public GetFormData(ILogger<GetFormData> logger)
        {
            _logger = logger;
        }

        [Function("GetFormData")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            // Read language from query string: ?lang=fr|en|zh  (default: fr)
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var lang = query["lang"]?.ToLower() ?? "fr";
            if (lang != "fr" && lang != "en" && lang != "zh") lang = "fr";

            var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
            var storageConn = Environment.GetEnvironmentVariable("AzureStorageConnectionString");
            var containerName = "warranty-pdfs";
            var sasExpiry = DateTimeOffset.UtcNow.AddMinutes(60);

            try
            {
                // ── 1. Query all DB data ──────────────────────────────────────────
                var categories   = new List<object>();
                var pianoTypes   = new List<object>();
                var benches      = new List<object>();
                var warrantyRows = new List<(int? typeId, int? categoryId, string blobName)>();
                string tradeUpBlob = null;

                using var conn = new SqlConnection(sqlConn);
                await conn.OpenAsync();

                // Categories
                using (var cmd = new SqlCommand(
                    "SELECT id, name_fr, name_en, name_zh, has_warranty, allows_manual_entry " +
                    "FROM dbo.PianoCategory", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        categories.Add(new
                        {
                            id                 = reader.GetInt32(0),
                            name_fr            = reader.GetString(1),
                            name_en            = reader.GetString(2),
                            name_zh            = reader.GetString(3),
                            has_warranty       = reader.GetBoolean(4),
                            allows_manual_entry = reader.GetBoolean(5)
                        });
                    }
                }

                // Piano types (active only)
                using (var cmd = new SqlCommand(
                    "SELECT id, category_id, name_fr, name_en, name_zh, brand_name " +
                    "FROM dbo.PianoType WHERE is_active = 1", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        pianoTypes.Add(new
                        {
                            id          = reader.GetInt32(0),
                            category_id = reader.GetInt32(1),
                            name_fr     = reader.GetString(2),
                            name_en     = reader.GetString(3),
                            name_zh     = reader.GetString(4),
                            brand_name  = reader.GetString(5)
                        });
                    }
                }

                // Benches
                using (var cmd = new SqlCommand(
                    "SELECT id, name FROM dbo.bench", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        benches.Add(new
                        {
                            id   = reader.GetInt32(0),
                            name = reader.IsDBNull(1) ? "" : reader.GetString(1)
                        });
                    }
                }

                // Warranty PDFs — fetch blob names for current language
                using (var cmd = new SqlCommand(
                    "SELECT type_id, category_id, blob_name " +
                    "FROM dbo.WarrantyPdf " +
                    "WHERE language = @lang AND is_active = 1", conn))
                {
                    cmd.Parameters.AddWithValue("@lang", lang);
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        int? typeId     = reader.IsDBNull(0) ? null : reader.GetInt32(0);
                        int? categoryId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                        string blobName = reader.GetString(2);
                        warrantyRows.Add((typeId, categoryId, blobName));
                    }
                }

                // TradeUp PDF for current language
                using (var cmd = new SqlCommand(
                    "SELECT TOP 1 blob_name FROM dbo.TradeUpPdf " +
                    "WHERE language = @lang AND is_active = 1", conn))
                {
                    cmd.Parameters.AddWithValue("@lang", lang);
                    var result = await cmd.ExecuteScalarAsync();
                    tradeUpBlob = result?.ToString();
                }

                // ── 2. Generate SAS URLs ──────────────────────────────────────────
                var containerClient = new BlobContainerClient(storageConn, containerName);

                // Warranty PDFs — build lookup: { "type_5": "https://...sas", "cat_3": "https://...sas" }
                var warrantyUrls = new Dictionary<string, string>();
                foreach (var (typeId, categoryId, blobName) in warrantyRows)
                {
                    var sasUrl = GenerateSasUrl(containerClient, blobName, sasExpiry);
                    if (typeId.HasValue)
                        warrantyUrls[$"new_piano_warranty_{typeId}"] = sasUrl;
                    else if (categoryId.HasValue)
                        warrantyUrls["used_piano"] = sasUrl;
                }

                // TradeUp PDF SAS URL
                string tradeUpUrl = null;
                if (!string.IsNullOrEmpty(tradeUpBlob))
                    tradeUpUrl = GenerateSasUrl(containerClient, tradeUpBlob, sasExpiry);

                // ── 3. Build response ─────────────────────────────────────────────
                var payload = new
                {
                    categories,
                    pianoTypes,
                    benches,
                    pdfs = new
                    {
                        warranty = warrantyUrls,   // keys: "type_1", "type_2", "cat_3" etc.
                        tradeup  = tradeUpUrl
                    }
                };

                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                await response.WriteStringAsync(JsonSerializer.Serialize(payload));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFormData failed");
                var errResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync($"{{\"error\":\"{ex.Message}\"}}");
                return errResponse;
            }
        }

        // ── Helper: generate a read-only SAS URL for a blob ──────────────────────
        private static string GenerateSasUrl(
            BlobContainerClient container, string blobName, DateTimeOffset expiry)
        {
            var blobClient = container.GetBlobClient(blobName);
            var sasUri = blobClient.GenerateSasUri(
                BlobSasPermissions.Read,
                expiry);
            return sasUri.ToString();
        }
    }
}