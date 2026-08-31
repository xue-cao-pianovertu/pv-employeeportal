using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

namespace PV.AZFunction;

public class SubmitAcademyRegistration
{
    private readonly ILogger<SubmitAcademyRegistration> _logger;
    public SubmitAcademyRegistration(ILogger<SubmitAcademyRegistration> logger) => _logger = logger;

    [Function("SubmitAcademyRegistration")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        _logger.LogInformation("SubmitAcademyRegistration triggered.");

        AcademyRegistrationPayload? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<AcademyRegistrationPayload>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
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

            // ── 1. Generate ref_id: APV-YYYY-MM-DD-XXXX ──────────────────────
            var now     = DateTime.UtcNow;
            var dateStr = now.ToString("yyyy-MM-dd");
            var prefix  = $"APV-{dateStr}-";
            var seqCmd  = new SqlCommand(
                "SELECT ISNULL(MAX(CAST(SUBSTRING(ref_id, 16, LEN(ref_id) - 15) AS INT)), 0) + 1 " +
                "FROM dbo.AcademyRegistrations " +
                "WHERE ref_id LIKE @prefix", conn);
            seqCmd.Parameters.AddWithValue("@prefix", $"{prefix}%");
            var seq   = Convert.ToInt32(await seqCmd.ExecuteScalarAsync() ?? 1);
            var refId = $"{prefix}{seq:D4}";

            // ── 2. Upload signature to blob storage ───────────────────────────
            var containerClient = new BlobContainerClient(storageConn, "signatures");
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            string? signatureBlobName = null;
            if (!string.IsNullOrEmpty(payload.SignatureData))
            {
                if (payload.SignatureType == "drawn")
                {
                    var base64 = payload.SignatureData;
                    var comma  = base64.IndexOf(',');
                    if (comma >= 0) base64 = base64[(comma + 1)..];

                    var bytes = Convert.FromBase64String(base64);
                    signatureBlobName = $"{refId}-signature.png";
                    var blobClient = containerClient.GetBlobClient(signatureBlobName);
                    using var ms   = new MemoryStream(bytes);
                    await blobClient.UploadAsync(ms, new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = "image/png" }
                    });
                }
                else
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

            // ── 3. Create or retrieve student account ─────────────────────────
            string? clientUsername = null;
            string? clientPassword = null;
            bool isNewAccount      = false;
            int studentUserId      = 0;

            var email = payload.Email ?? "";

            // Look for an existing student account only — do NOT reuse a 'customer' account
            var checkCmd = new SqlCommand(
                "SELECT id, username, password FROM dbo.Users WHERE username = @email AND role = 'student'", conn);
            checkCmd.Parameters.AddWithValue("@email", email);

            using (var cr = await checkCmd.ExecuteReaderAsync())
            {
                if (await cr.ReadAsync())
                {
                    studentUserId  = cr.GetInt32(0);
                    clientUsername = cr.GetString(1);
                    clientPassword = cr.IsDBNull(2) ? null : cr.GetString(2);
                }
            }

            if (studentUserId == 0 && !string.IsNullOrEmpty(email))
            {
                clientPassword = "apvstudent2026";
                isNewAccount   = true;
                var fullName   = $"{payload.StudentFirstName} {payload.StudentLastName}".Trim();

                // If the email is already taken (e.g. piano customer), append ".apv" to avoid UNIQUE conflict
                var usernameToUse = email;
                var conflictCmd   = new SqlCommand(
                    "SELECT COUNT(1) FROM dbo.Users WHERE username = @username", conn);
                conflictCmd.Parameters.AddWithValue("@username", email);
                var takenCount = (int)(await conflictCmd.ExecuteScalarAsync() ?? 0);
                if (takenCount > 0)
                    usernameToUse = email + ".apv";

                clientUsername = usernameToUse;
                var createCmd  = new SqlCommand(
                    "INSERT INTO dbo.Users (username, password, role, full_name) " +
                    "OUTPUT INSERTED.id " +
                    "VALUES (@username, @password, 'student', @fullName)", conn);
                createCmd.Parameters.AddWithValue("@username", usernameToUse);
                createCmd.Parameters.AddWithValue("@password", clientPassword);
                createCmd.Parameters.AddWithValue("@fullName", fullName);
                studentUserId = (int)(await createCmd.ExecuteScalarAsync() ?? 0);
                _logger.LogInformation("Student account created: {Email}", usernameToUse);
            }

            // ── 4. Insert into AcademyRegistrations ───────────────────────────
            var insertCmd = new SqlCommand(@"
                INSERT INTO dbo.AcademyRegistrations (
                    ref_id, student_first_name, student_last_name,
                    child_first_name, child_last_name,
                    email, phone, emergency_contact_name, emergency_contact_phone,
                    language, level, preferred_teacher, instrument,
                    availabilities, start_preference,
                    policy_accepted, signature_blob_name,
                    student_user_id
                ) VALUES (
                    @refId, @studentFirstName, @studentLastName,
                    @childFirstName, @childLastName,
                    @email, @phone, @emergencyContactName, @emergencyContactPhone,
                    @language, @level, @preferredTeacher, @instrument,
                    @availabilities, @startPreference,
                    @policyAccepted, @signatureBlobName,
                    @studentUserId
                )", conn);

            insertCmd.Parameters.AddWithValue("@refId",                refId);
            insertCmd.Parameters.AddWithValue("@studentFirstName",     payload.StudentFirstName  ?? "");
            insertCmd.Parameters.AddWithValue("@studentLastName",      payload.StudentLastName   ?? "");
            insertCmd.Parameters.AddWithValue("@childFirstName",       (object?)payload.ChildFirstName        ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@childLastName",        (object?)payload.ChildLastName         ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@email",                email);
            insertCmd.Parameters.AddWithValue("@phone",                (object?)payload.Phone                 ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@emergencyContactName", (object?)payload.EmergencyContactName  ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@emergencyContactPhone",(object?)payload.EmergencyContactPhone ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@language",             payload.Language?.ToUpper()            ?? "FR");
            insertCmd.Parameters.AddWithValue("@level",                (object?)payload.Level                 ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@preferredTeacher",     (object?)payload.PreferredTeacher      ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@instrument",           (object?)payload.Instrument            ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@availabilities",       (object?)payload.Availabilities        ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@startPreference",      (object?)payload.StartPreference       ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@policyAccepted",       payload.PolicyAccepted ? 1 : 0);
            insertCmd.Parameters.AddWithValue("@signatureBlobName",    (object?)signatureBlobName             ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@studentUserId",        studentUserId > 0 ? studentUserId : DBNull.Value);

            await insertCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Academy registration saved: {RefId}", refId);
            return new OkObjectResult(new
            {
                ref_id          = refId,
                success         = true,
                new_account     = isNewAccount,
                client_username = clientUsername,
                client_password = clientPassword
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitAcademyRegistration failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static string GeneratePassword(int length = 8)
    {
        const string chars = "abcdefghijkmnpqrstuvwxyz23456789";
        var rng = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}

public class AcademyRegistrationPayload
{
    public string? StudentFirstName      { get; set; }
    public string? StudentLastName       { get; set; }
    public string? ChildFirstName        { get; set; }
    public string? ChildLastName         { get; set; }
    public string? Email                 { get; set; }
    public string? Phone                 { get; set; }
    public string? EmergencyContactName  { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? Language              { get; set; }
    public string? Level                 { get; set; }
    public string? PreferredTeacher      { get; set; }
    public string? Instrument            { get; set; }
    public string? Availabilities        { get; set; }
    public string? StartPreference       { get; set; }
    public bool    PolicyAccepted        { get; set; }
    public string? SignatureType         { get; set; }
    public string? SignatureData         { get; set; }
}
