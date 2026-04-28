using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PV.AZFunction;

public class UpdateClientInfo
{
    private readonly ILogger<UpdateClientInfo> _logger;
    public UpdateClientInfo(ILogger<UpdateClientInfo> logger) => _logger = logger;

    [Function("UpdateClientInfo")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        ClientUpdate? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ClientUpdate>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (body?.Id == null)
            return new BadRequestObjectResult(new { error = "id required" });

        var changedBy = UpdateRegistration.GetUsername(req);
        var sqlConn   = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // ── Read current values for audit diff ────────────────────
            var changes = new Dictionary<string, string?[]>();
            using (var selCmd = new SqlCommand(@"
                SELECT customer_first_name, customer_last_name, customer_email,
                       customer_phone1, customer_phone2, heard_from, referred_by_teacher
                FROM dbo.Registrations WHERE id = @id", conn))
            {
                selCmd.Parameters.AddWithValue("@id", body.Id);
                using var reader = await selCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return new NotFoundObjectResult(new { error = "Registration not found" });

                Diff(changes, "customer_first_name", reader["customer_first_name"] as string, body.CustomerFirstName);
                Diff(changes, "customer_last_name",  reader["customer_last_name"]  as string, body.CustomerLastName);
                Diff(changes, "customer_email",      reader["customer_email"]       as string, body.CustomerEmail);
                Diff(changes, "customer_phone1",     reader["customer_phone1"]      as string, body.CustomerPhone1);
                Diff(changes, "customer_phone2",     reader["customer_phone2"]      as string, body.CustomerPhone2);
                Diff(changes, "heard_from",          reader["heard_from"]           as string, body.HeardFrom);
                Diff(changes, "referred_by_teacher", reader["referred_by_teacher"]  as string, body.ReferredByTeacher);
            }

            // ── Update ───────────────────────────────────────────────
            var cmd = new SqlCommand(@"
                UPDATE dbo.Registrations SET
                    customer_first_name = @firstName,
                    customer_last_name  = @lastName,
                    customer_email      = @email,
                    customer_phone1     = @phone1,
                    customer_phone2     = @phone2,
                    heard_from           = @heardFrom,
                    referred_by_teacher  = @referredByTeacher
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id",        body.Id);
            cmd.Parameters.AddWithValue("@firstName",  (object?)body.CustomerFirstName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@lastName",   (object?)body.CustomerLastName  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@email",      (object?)body.CustomerEmail     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone1",     (object?)body.CustomerPhone1    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@phone2",     (object?)body.CustomerPhone2    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@heardFrom",         (object?)body.HeardFrom          ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@referredByTeacher", (object?)body.ReferredByTeacher  ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            // ── Audit log ────────────────────────────────────────────
            if (changes.Count > 0)
                await UpdateRegistration.WriteAuditLog(conn, body.Id.Value, changedBy, "client", changes);

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateClientInfo failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static void Diff(Dictionary<string, string?[]> changes, string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
            changes[field] = [oldVal, newVal];
    }
}

public class ClientUpdate
{
    public int?     Id                  { get; set; }
    public string?  CustomerFirstName   { get; set; }
    public string?  CustomerLastName    { get; set; }
    public string?  CustomerEmail       { get; set; }
    public string?  CustomerPhone1      { get; set; }
    public string?  CustomerPhone2      { get; set; }
    public string?  HeardFrom           { get; set; }
    public string?  ReferredByTeacher   { get; set; }
}
