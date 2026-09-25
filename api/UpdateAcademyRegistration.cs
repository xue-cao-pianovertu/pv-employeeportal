using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace PV.AZFunction;

public class UpdateAcademyRegistration
{
    private readonly ILogger<UpdateAcademyRegistration> _logger;
    public UpdateAcademyRegistration(ILogger<UpdateAcademyRegistration> logger) => _logger = logger;

    [Function("UpdateAcademyRegistration")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "apvadmin")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        AcademyUpdate? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<AcademyUpdate>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (body?.Id == null)
            return new BadRequestObjectResult(new { error = "id required" });

        var changedBy = GetUsername(req);
        var sqlConn   = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // ── Read current values for audit diff ────────────────────────
            var changes = new Dictionary<string, string?[]>();
            using (var selCmd = new SqlCommand(@"
                SELECT student_first_name, student_last_name,
                       child_first_name, child_last_name,
                       email, phone, emergency_contact_name, emergency_contact_phone,
                       language, level, preferred_teacher, instrument,
                       availabilities, start_preference, staff_notes,
                       is_returning_student
                FROM dbo.AcademyRegistrations WHERE id = @id", conn))
            {
                selCmd.Parameters.AddWithValue("@id", body.Id);
                using var r = await selCmd.ExecuteReaderAsync();
                if (!await r.ReadAsync())
                    return new NotFoundObjectResult(new { error = "Registration not found" });

                Diff(changes, "student_first_name",      r["student_first_name"]      as string, body.StudentFirstName);
                Diff(changes, "student_last_name",       r["student_last_name"]       as string, body.StudentLastName);
                Diff(changes, "child_first_name",        r["child_first_name"]        as string, body.ChildFirstName);
                Diff(changes, "child_last_name",         r["child_last_name"]         as string, body.ChildLastName);
                Diff(changes, "email",                   r["email"]                   as string, body.Email);
                Diff(changes, "phone",                   r["phone"]                   as string, body.Phone);
                Diff(changes, "emergency_contact_name",  r["emergency_contact_name"]  as string, body.EmergencyContactName);
                Diff(changes, "emergency_contact_phone", r["emergency_contact_phone"] as string, body.EmergencyContactPhone);
                Diff(changes, "language",                r["language"]                as string, body.Language);
                Diff(changes, "level",                   r["level"]                   as string, body.Level);
                Diff(changes, "preferred_teacher",       r["preferred_teacher"]       as string, body.PreferredTeacher);
                Diff(changes, "instrument",              r["instrument"]              as string, body.Instrument);
                Diff(changes, "availabilities",          r["availabilities"]          as string, body.Availabilities);
                Diff(changes, "start_preference",        r["start_preference"]        as string, body.StartPreference);
                Diff(changes, "staff_notes",             r["staff_notes"]             as string, body.StaffNotes);
                if (body.IsReturningStudent.HasValue)
                {
                    var oldBool = (bool)r["is_returning_student"];
                    var newBool = body.IsReturningStudent.Value;
                    if (oldBool != newBool)
                        changes["is_returning_student"] = [oldBool ? "1" : "0", newBool ? "1" : "0"];
                }
            }

            // ── Update ────────────────────────────────────────────────────
            var cmd = new SqlCommand(@"
                UPDATE dbo.AcademyRegistrations SET
                    student_first_name      = @studentFirstName,
                    student_last_name       = @studentLastName,
                    child_first_name        = @childFirstName,
                    child_last_name         = @childLastName,
                    email                   = @email,
                    phone                   = @phone,
                    emergency_contact_name  = @emergencyContactName,
                    emergency_contact_phone = @emergencyContactPhone,
                    language                = @language,
                    level                   = @level,
                    preferred_teacher       = @preferredTeacher,
                    instrument              = @instrument,
                    availabilities          = @availabilities,
                    start_preference        = @startPreference,
                    staff_notes             = @staffNotes,
                    is_returning_student    = ISNULL(@isReturningStudent, is_returning_student),
                    updated_at              = SYSUTCDATETIME()
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id",                    body.Id);
            cmd.Parameters.AddWithValue("@studentFirstName",      (object?)body.StudentFirstName      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@studentLastName",       (object?)body.StudentLastName       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@childFirstName",        (object?)body.ChildFirstName        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@childLastName",         (object?)body.ChildLastName         ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email",                 (object?)body.Email                 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone",                 (object?)body.Phone                 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@emergencyContactName",  (object?)body.EmergencyContactName  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@emergencyContactPhone", (object?)body.EmergencyContactPhone ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@language",              (object?)body.Language              ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@level",                 (object?)body.Level                 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@preferredTeacher",      (object?)body.PreferredTeacher      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@instrument",            (object?)body.Instrument            ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@availabilities",        (object?)body.Availabilities        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@startPreference",       (object?)body.StartPreference       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@staffNotes",            (object?)body.StaffNotes            ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@isReturningStudent",
                body.IsReturningStudent.HasValue ? (object)(body.IsReturningStudent.Value ? 1 : 0) : DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            if (changes.Count > 0)
                await WriteAcademyAuditLog(conn, body.Id.Value, changedBy, "student", changes);

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAcademyRegistration failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static void Diff(Dictionary<string, string?[]> changes, string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
            changes[field] = [oldVal, newVal];
    }

    internal static async Task WriteAcademyAuditLog(SqlConnection conn, int regId, string changedBy, string section, Dictionary<string, string?[]> changes)
    {
        var json = JsonSerializer.Serialize(changes.ToDictionary(
            kvp => kvp.Key,
            kvp => new { old = kvp.Value[0], @new = kvp.Value[1] }));

        var logCmd = new SqlCommand(@"
            INSERT INTO dbo.AcademyAuditLog (registration_id, changed_by, section, changes_json)
            VALUES (@regId, @changedBy, @section, @changes)", conn);
        logCmd.Parameters.AddWithValue("@regId",     regId);
        logCmd.Parameters.AddWithValue("@changedBy", changedBy);
        logCmd.Parameters.AddWithValue("@section",   section);
        logCmd.Parameters.AddWithValue("@changes",   json);
        await logCmd.ExecuteNonQueryAsync();
    }

    private static string? GetRole(HttpRequest req)
    {
        var auth = req.Headers["X-Token"].ToString();
        if (string.IsNullOrEmpty(auth)) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer ")) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            return jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        }
        catch { return null; }
    }

    private static string GetUsername(HttpRequest req)
    {
        var auth = req.Headers["X-Token"].ToString();
        if (!auth.StartsWith("Bearer ")) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer ")) return "inconnu";
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            return jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value ?? "inconnu";
        }
        catch { return "inconnu"; }
    }
}

public class AcademyUpdate
{
    public int?    Id                     { get; set; }
    public string? StudentFirstName       { get; set; }
    public string? StudentLastName        { get; set; }
    public string? ChildFirstName         { get; set; }
    public string? ChildLastName          { get; set; }
    public string? Email                  { get; set; }
    public string? Phone                  { get; set; }
    public string? EmergencyContactName   { get; set; }
    public string? EmergencyContactPhone  { get; set; }
    public string? Language               { get; set; }
    public string? Level                  { get; set; }
    public string? PreferredTeacher       { get; set; }
    public string? Instrument             { get; set; }
    public string? Availabilities         { get; set; }
    public string? StartPreference        { get; set; }
    public string? StaffNotes             { get; set; }
    public bool?   IsReturningStudent     { get; set; }
}
