using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PV.AZFunction;

public class UpdateLead
{
    private readonly ILogger<UpdateLead> _logger;
    public UpdateLead(ILogger<UpdateLead> logger) => _logger = logger;

    private static readonly HashSet<string> ValidStatuses = new()
    {
        "new", "contacted", "interested", "to_follow_up", "not_interested", "converted"
    };

    private static readonly HashSet<string> ValidContactTypes = new()
    {
        "visit", "call", "email", "web", "other"
    };

    [Function("UpdateLead")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        LeadUpdate? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<LeadUpdate>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (body?.Id == null)
            return new BadRequestObjectResult(new { error = "id required" });

        if (body.LeadStatus != null && !ValidStatuses.Contains(body.LeadStatus))
            return new BadRequestObjectResult(new { error = "Invalid lead_status" });

        if (body.ContactType != null && !ValidContactTypes.Contains(body.ContactType))
            return new BadRequestObjectResult(new { error = "Invalid contact_type" });

        var changedBy = UpdateRegistration.GetUsername(req);
        var sqlConn   = Environment.GetEnvironmentVariable("SqlConnectionString");

        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // ── Read current values for audit diff ────────────────────
            string? oldStatus, oldStaffNotes, oldFirstName, oldLastName, oldEmail;
            string? oldPhone, oldPianoInterest, oldContactType, oldContactDate, oldCustomerNotes;

            using (var sel = new SqlCommand(@"
                SELECT lead_status, staff_notes, first_name, last_name, email,
                       phone, piano_interest, contact_type,
                       CONVERT(NVARCHAR(10), contact_date, 23) AS contact_date,
                       customer_notes
                FROM dbo.Leads WHERE id = @id", conn))
            {
                sel.Parameters.AddWithValue("@id", body.Id);
                using var reader = await sel.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return new NotFoundObjectResult(new { error = "Lead not found" });

                oldStatus        = reader["lead_status"]     as string;
                oldStaffNotes    = reader["staff_notes"]     as string;
                oldFirstName     = reader["first_name"]      as string;
                oldLastName      = reader["last_name"]       as string;
                oldEmail         = reader["email"]           as string;
                oldPhone         = reader["phone"]           as string;
                oldPianoInterest = reader["piano_interest"]  as string;
                oldContactType   = reader["contact_type"]    as string;
                oldContactDate   = reader["contact_date"]    as string;
                oldCustomerNotes = reader["customer_notes"]  as string;
            }

            // ── Apply update ──────────────────────────────────────────
            var cmd = new SqlCommand(@"
                UPDATE dbo.Leads SET
                    lead_status    = COALESCE(@leadStatus,    lead_status),
                    staff_notes    = @staffNotes,
                    first_name     = COALESCE(@firstName,     first_name),
                    last_name      = COALESCE(@lastName,      last_name),
                    email          = COALESCE(@email,         email),
                    phone          = @phone,
                    piano_interest = @pianoInterest,
                    contact_type   = COALESCE(@contactType,   contact_type),
                    contact_date   = COALESCE(@contactDate,   contact_date),
                    customer_notes = @customerNotes
                WHERE id = @id",
                conn);

            cmd.Parameters.AddWithValue("@id",            body.Id);
            cmd.Parameters.AddWithValue("@leadStatus",    (object?)body.LeadStatus    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@staffNotes",    (object?)body.StaffNotes    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@firstName",     (object?)body.FirstName     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lastName",      (object?)body.LastName      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email",         (object?)body.Email         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone",         (object?)body.Phone         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pianoInterest", (object?)body.PianoInterest ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@contactType",   (object?)body.ContactType   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@customerNotes", (object?)body.CustomerNotes ?? DBNull.Value);

            if (body.ContactDate != null && DateOnly.TryParse(body.ContactDate, out var d))
                cmd.Parameters.AddWithValue("@contactDate", d.ToString("yyyy-MM-dd"));
            else
                cmd.Parameters.AddWithValue("@contactDate", DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            // ── Diff and log ──────────────────────────────────────────
            // For COALESCE fields: effective new value = body value ?? old value (no change if body is null)
            // For always-overwrite fields: effective new value = body value (even null)
            var changes = new Dictionary<string, string?[]>();
            Diff(changes, "lead_status",    oldStatus,        body.LeadStatus    ?? oldStatus);
            Diff(changes, "staff_notes",    oldStaffNotes,    body.StaffNotes);
            Diff(changes, "first_name",     oldFirstName,     body.FirstName     ?? oldFirstName);
            Diff(changes, "last_name",      oldLastName,      body.LastName      ?? oldLastName);
            Diff(changes, "email",          oldEmail,         body.Email         ?? oldEmail);
            Diff(changes, "phone",          oldPhone,         body.Phone);
            Diff(changes, "piano_interest", oldPianoInterest, body.PianoInterest);
            Diff(changes, "contact_type",   oldContactType,   body.ContactType   ?? oldContactType);
            Diff(changes, "contact_date",   oldContactDate,   body.ContactDate   ?? oldContactDate);
            Diff(changes, "customer_notes", oldCustomerNotes, body.CustomerNotes);

            await UpdateRegistration.WriteLeadAuditLog(conn, body.Id.Value, changedBy, "lead", changes);

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateLead DB error");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static void Diff(Dictionary<string, string?[]> changes, string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
            changes[field] = [oldVal, newVal];
    }
}

public class LeadUpdate
{
    public int?    Id            { get; set; }
    public string? LeadStatus    { get; set; }
    public string? StaffNotes    { get; set; }
    public string? FirstName     { get; set; }
    public string? LastName      { get; set; }
    public string? Email         { get; set; }
    public string? Phone         { get; set; }
    public string? PianoInterest { get; set; }
    public string? ContactType   { get; set; }
    public string? ContactDate   { get; set; }
    public string? CustomerNotes { get; set; }
}
