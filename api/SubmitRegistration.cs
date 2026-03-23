using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PV.AZFunction;

public class SubmitRegistration
{
    private readonly ILogger<SubmitRegistration> _logger;

    public SubmitRegistration(ILogger<SubmitRegistration> logger)
    {
        _logger = logger;
    }

    [Function("SubmitRegistration")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        _logger.LogInformation("SubmitRegistration triggered.");

        RegistrationPayload? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<RegistrationPayload>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy    = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid JSON payload");
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (payload == null)
            return new BadRequestObjectResult(new { error = "Empty payload" });

        var sqlConn     = Environment.GetEnvironmentVariable("SqlConnectionString");
        var storageConn = Environment.GetEnvironmentVariable("AzureStorageConnectionString");

        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // ── 1. Generate server-side ref_id: PV-YYYY-MM-DD-XXXX ───────────
            var now     = DateTime.UtcNow;
            var dateStr = now.ToString("yyyy-MM-dd");
            var prefix  = $"PV-{dateStr}-";
            var seqCmd  = new SqlCommand(
                "SELECT ISNULL(MAX(CAST(SUBSTRING(ref_id, 15, LEN(ref_id) - 14) AS INT)), 0) + 1 " +
                "FROM dbo.Registrations " +
                "WHERE ref_id LIKE @prefix", conn);
            seqCmd.Parameters.AddWithValue("@prefix", $"{prefix}%");
            var seq   = Convert.ToInt32(await seqCmd.ExecuteScalarAsync() ?? 1);
            var refId = $"{prefix}{seq:D4}";

            // ── 2. Look up warranty PDF blob name from DB ─────────────────────
            var lang = payload.Language?.ToLower() ?? "fr";
            if (lang != "fr" && lang != "en" && lang != "zh") lang = "fr";

            string? warrantyBlob = null;
            if (payload.PianoTypeId.HasValue)
            {
                var wCmd = new SqlCommand(
                    "SELECT TOP 1 blob_name FROM dbo.WarrantyPdf " +
                    "WHERE type_id = @typeId AND language = @lang AND is_active = 1", conn);
                wCmd.Parameters.AddWithValue("@typeId", payload.PianoTypeId.Value);
                wCmd.Parameters.AddWithValue("@lang", lang);
                warrantyBlob = (await wCmd.ExecuteScalarAsync())?.ToString();
            }
            else if (payload.PianoCategoryId.HasValue)
            {
                var wCmd = new SqlCommand(
                    "SELECT TOP 1 blob_name FROM dbo.WarrantyPdf " +
                    "WHERE category_id = @catId AND language = @lang AND is_active = 1", conn);
                wCmd.Parameters.AddWithValue("@catId", payload.PianoCategoryId.Value);
                wCmd.Parameters.AddWithValue("@lang", lang);
                warrantyBlob = (await wCmd.ExecuteScalarAsync())?.ToString();
            }

            // ── 3. Look up tradeup PDF blob name from DB ──────────────────────
            var tuCmd = new SqlCommand(
                "SELECT TOP 1 blob_name FROM dbo.TradeUpPdf " +
                "WHERE language = @lang AND is_active = 1", conn);
            tuCmd.Parameters.AddWithValue("@lang", lang);
            var tradeupBlob = (await tuCmd.ExecuteScalarAsync())?.ToString();

            // ── 4. Upload signature to blob storage ───────────────────────────
            var containerClient = new BlobContainerClient(storageConn, "signatures");
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            string? signatureBlobName = null;
            if (!string.IsNullOrEmpty(payload.SignatureData))
            {
                if (payload.SignatureType == "drawn")
                {
                    // Strip "data:image/png;base64," prefix
                    var base64 = payload.SignatureData;
                    var comma  = base64.IndexOf(',');
                    if (comma >= 0) base64 = base64[(comma + 1)..];

                    var bytes      = Convert.FromBase64String(base64);
                    signatureBlobName = $"{refId}-signature.png";
                    var blobClient = containerClient.GetBlobClient(signatureBlobName);
                    using var ms   = new MemoryStream(bytes);
                    await blobClient.UploadAsync(ms, new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" }
                    });
                }
                else // typed
                {
                    signatureBlobName = $"{refId}-signature.txt";
                    var blobClient    = containerClient.GetBlobClient(signatureBlobName);
                    using var ms      = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload.SignatureData));
                    await blobClient.UploadAsync(ms, new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "text/plain; charset=utf-8" }
                    });
                }
            }

            // ── 5. Insert into Registrations ──────────────────────────────────
            var insertCmd = new SqlCommand(@"
                INSERT INTO dbo.Registrations (
                    ref_id, language,
                    customer_last_name, customer_first_name, customer_email,
                    customer_phone1, customer_phone2, heard_from,
                    delivery_street, delivery_apt, delivery_city, delivery_province, delivery_postal,
                    within_40km, delivery_floor, delivery_elevator,
                    steps_outside, steps_inside, stair_turns, mover_notes,
                    collect_piano, collect_desc, recycle_piano, recycle_desc, crane_required,
                    delivery_asap, delivery_date, delivery_notes, surcharge_flag,
                    piano_category_id, piano_type_id, piano_make, piano_model, piano_serial, piano_color,
                    purchase_date, accessories, bench_model_id, bench_notes, piano_notes,
                    humidity_confirmed, warranty_pdf_blob, tradeup_pdf_blob,
                    signature_type, signature_blob_name,
                    invoice_number, from_location, old_piano_dest,
                    surcharge_amount, cheque_to_collect, google_review, fully_paid, staff_notes
                ) VALUES (
                    @refId, @language,
                    @customerLastName, @customerFirstName, @customerEmail,
                    @customerPhone1, @customerPhone2, @heardFrom,
                    @deliveryStreet, @deliveryApt, @deliveryCity, @deliveryProvince, @deliveryPostal,
                    @within40km, @deliveryFloor, @deliveryElevator,
                    @stepsOutside, @stepsInside, @stairTurns, @moverNotes,
                    @collectPiano, @collectDesc, @recyclePiano, @recycleDesc, @craneRequired,
                    @deliveryAsap, @deliveryDate, @deliveryNotes, @surchargeFlag,
                    @pianoCategoryId, @pianoTypeId, @pianoMake, @pianoModel, @pianoSerial, @pianoColor,
                    @purchaseDate, @accessories, @benchModelId, @benchNotes, @pianoNotes,
                    @humidityConfirmed, @warrantyPdfBlob, @tradeupPdfBlob,
                    @signatureType, @signatureBlobName,
                    @invoiceNumber, @fromLocation, @oldPianoDest,
                    @surchargeAmount, @chequeToCollect, @googleReview, @fullyPaid, @staffNotes
                )", conn);

            insertCmd.Parameters.AddWithValue("@refId",              refId);
            insertCmd.Parameters.AddWithValue("@language",           payload.Language?.ToUpper() ?? "FR");
            insertCmd.Parameters.AddWithValue("@customerLastName",   payload.CustomerLastName  ?? "");
            insertCmd.Parameters.AddWithValue("@customerFirstName",  payload.CustomerFirstName ?? "");
            insertCmd.Parameters.AddWithValue("@customerEmail",      payload.CustomerEmail     ?? "");
            insertCmd.Parameters.AddWithValue("@customerPhone1",     (object?)payload.CustomerPhone1  ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@customerPhone2",     (object?)payload.CustomerPhone2  ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@heardFrom",          (object?)payload.HeardFrom       ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@deliveryStreet",     payload.DeliveryStreet   ?? "");
            insertCmd.Parameters.AddWithValue("@deliveryApt",        (object?)payload.DeliveryApt      ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@deliveryCity",       (object?)payload.DeliveryCity     ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@deliveryProvince",   (object?)payload.DeliveryProvince ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@deliveryPostal",     (object?)payload.DeliveryPostal   ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@within40km",         payload.Within40km);
            insertCmd.Parameters.AddWithValue("@deliveryFloor",      (object?)payload.DeliveryFloor    ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@deliveryElevator",   payload.DeliveryElevator);
            insertCmd.Parameters.AddWithValue("@stepsOutside",       payload.StepsOutside);
            insertCmd.Parameters.AddWithValue("@stepsInside",        payload.StepsInside);
            insertCmd.Parameters.AddWithValue("@stairTurns",         payload.StairTurns);
            insertCmd.Parameters.AddWithValue("@moverNotes",         (object?)payload.MoverNotes       ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@collectPiano",       payload.CollectPiano);
            insertCmd.Parameters.AddWithValue("@collectDesc",        (object?)payload.CollectDesc      ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@recyclePiano",       payload.RecyclePiano);
            insertCmd.Parameters.AddWithValue("@recycleDesc",        (object?)payload.RecycleDesc      ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@craneRequired",      payload.CraneRequired);
            insertCmd.Parameters.AddWithValue("@deliveryAsap",       payload.DeliveryAsap);
            insertCmd.Parameters.AddWithValue("@deliveryDate",       (object?)payload.DeliveryDate     ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@deliveryNotes",      (object?)payload.DeliveryNotes    ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@surchargeFlag",      payload.SurchargeFlag);
            insertCmd.Parameters.AddWithValue("@pianoCategoryId",    (object?)payload.PianoCategoryId  ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@pianoTypeId",        (object?)payload.PianoTypeId      ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@pianoMake",          (object?)payload.PianoMake        ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@pianoModel",         (object?)payload.PianoModel       ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@pianoSerial",        (object?)payload.PianoSerial ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@pianoColor",         (object?)payload.PianoColor       ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@purchaseDate",       payload.PurchaseDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd"));
            insertCmd.Parameters.AddWithValue("@accessories",        (object?)payload.Accessories      ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@benchModelId",       (object?)payload.BenchModelId     ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@benchNotes",         (object?)payload.BenchNotes       ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@pianoNotes",         (object?)payload.PianoNotes       ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@humidityConfirmed",  payload.HumidityConfirmed);
            insertCmd.Parameters.AddWithValue("@warrantyPdfBlob",    (object?)warrantyBlob             ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@tradeupPdfBlob",     (object?)tradeupBlob              ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@signatureType",      payload.SignatureType ?? "drawn");
            insertCmd.Parameters.AddWithValue("@signatureBlobName",  (object?)signatureBlobName        ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@invoiceNumber",      (object?)payload.InvoiceNumber    ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@fromLocation",       (object?)payload.FromLocation     ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@oldPianoDest",       (object?)payload.OldPianoDest     ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@surchargeAmount",    payload.SurchargeAmount);
            insertCmd.Parameters.AddWithValue("@chequeToCollect",    payload.ChequeToCollect);
            insertCmd.Parameters.AddWithValue("@googleReview",       payload.GoogleReview);
            insertCmd.Parameters.AddWithValue("@fullyPaid",          payload.FullyPaid);
            insertCmd.Parameters.AddWithValue("@staffNotes",         (object?)payload.StaffNotes       ?? DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync();

            // ── 6. Create customer account if first registration ──────────────
            string? generatedPassword = null;
            var checkCmd = new SqlCommand(
                "SELECT COUNT(1) FROM dbo.Users WHERE username = @email", conn);
            checkCmd.Parameters.AddWithValue("@email", payload.CustomerEmail ?? "");
            var existing = Convert.ToInt32(await checkCmd.ExecuteScalarAsync() ?? 0);

            if (existing == 0 && !string.IsNullOrEmpty(payload.CustomerEmail))
            {
                generatedPassword = GeneratePassword();
                var fullName = $"{payload.CustomerFirstName} {payload.CustomerLastName}".Trim();
                var createUserCmd = new SqlCommand(
                    "INSERT INTO dbo.Users (username, password, role, full_name) " +
                    "VALUES (@username, @password, 'customer', @fullName)", conn);
                createUserCmd.Parameters.AddWithValue("@username", payload.CustomerEmail);
                createUserCmd.Parameters.AddWithValue("@password", generatedPassword);
                createUserCmd.Parameters.AddWithValue("@fullName", fullName);
                await createUserCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("Customer account created: {Email}", payload.CustomerEmail);
            }

            _logger.LogInformation("Registration saved: {RefId}", refId);
            return new OkObjectResult(new
            {
                ref_id           = refId,
                success          = true,
                new_account      = generatedPassword != null,
                client_username  = generatedPassword != null ? payload.CustomerEmail : null,
                client_password  = generatedPassword
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitRegistration failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = new byte[8];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}

public class RegistrationPayload
{
    public string?  Language            { get; set; }
    public string?  CustomerLastName    { get; set; }
    public string?  CustomerFirstName   { get; set; }
    public string?  CustomerEmail       { get; set; }
    public string?  CustomerPhone1      { get; set; }
    public string?  CustomerPhone2      { get; set; }
    public string?  HeardFrom           { get; set; }
    public string?  DeliveryStreet      { get; set; }
    public string?  DeliveryApt         { get; set; }
    public string?  DeliveryCity        { get; set; }
    public string?  DeliveryProvince    { get; set; }
    public string?  DeliveryPostal      { get; set; }
    public bool     Within40km          { get; set; }
    public string?  DeliveryFloor       { get; set; }
    public bool     DeliveryElevator    { get; set; }
    public int      StepsOutside        { get; set; }
    public int      StepsInside         { get; set; }
    public int      StairTurns          { get; set; }
    public string?  MoverNotes          { get; set; }
    public bool     CollectPiano        { get; set; }
    public string?  CollectDesc         { get; set; }
    public bool     RecyclePiano        { get; set; }
    public string?  RecycleDesc         { get; set; }
    public bool     CraneRequired       { get; set; }
    public bool     DeliveryAsap        { get; set; }
    public string?  DeliveryDate        { get; set; }
    public string?  DeliveryNotes       { get; set; }
    public bool     SurchargeFlag       { get; set; }
    public int?     PianoCategoryId     { get; set; }
    public int?     PianoTypeId         { get; set; }
    public string?  PianoMake           { get; set; }
    public string?  PianoModel          { get; set; }
    public string?  PianoSerial         { get; set; }
    public string?  PianoColor          { get; set; }
    public string?  PurchaseDate        { get; set; }
    public string?  Accessories         { get; set; }
    public int?     BenchModelId        { get; set; }
    public string?  BenchNotes          { get; set; }
    public string?  PianoNotes          { get; set; }
    public bool     HumidityConfirmed   { get; set; }
    public string?  SignatureType       { get; set; }   // "drawn" | "typed"
    public string?  SignatureData       { get; set; }   // base64 data URL or typed name
    public string?  InvoiceNumber       { get; set; }
    public string?  FromLocation        { get; set; }
    public string?  OldPianoDest        { get; set; }
    public decimal  SurchargeAmount     { get; set; }
    public bool     ChequeToCollect     { get; set; }
    public bool     GoogleReview        { get; set; }
    public bool     FullyPaid           { get; set; }
    public string?  StaffNotes          { get; set; }
}
