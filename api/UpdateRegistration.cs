using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace PV.AZFunction;

public class UpdateRegistration
{
    private readonly ILogger<UpdateRegistration> _logger;
    public UpdateRegistration(ILogger<UpdateRegistration> logger) => _logger = logger;

    [Function("UpdateRegistration")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        StaffUpdate? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<StaffUpdate>(
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

            // ── Read current values for audit diff ────────────────────
            var changes = new Dictionary<string, string?[]>();
            using (var selCmd = new SqlCommand(@"
                SELECT invoice_number, from_location, old_piano_dest, surcharge_amount,
                       cheque_to_collect, google_review, fully_paid, staff_notes,
                       piano_serial, payment_status, delivery_status, price,
                       tuning_sessions_agreed, bench_model_id
                FROM dbo.Registrations WHERE id = @id", conn))
            {
                selCmd.Parameters.AddWithValue("@id", body.Id);
                using var reader = await selCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return new NotFoundObjectResult(new { error = "Registration not found" });

                string?  oldInvoice   = reader["invoice_number"]  as string;
                string?  oldFrom      = reader["from_location"]    as string;
                string?  oldDest      = reader["old_piano_dest"]   as string;
                decimal  oldSurcharge = reader["surcharge_amount"] == DBNull.Value ? 0m : (decimal)reader["surcharge_amount"];
                bool     oldCheque    = reader["cheque_to_collect"] != DBNull.Value && (bool)reader["cheque_to_collect"];
                bool     oldGoogle    = reader["google_review"]     != DBNull.Value && (bool)reader["google_review"];
                bool     oldPaid      = reader["fully_paid"]        != DBNull.Value && (bool)reader["fully_paid"];
                string?  oldNotes          = reader["staff_notes"]       as string;
                string?  oldSerial         = reader["piano_serial"]      as string;
                string?  oldPaymentStatus  = reader["payment_status"]    as string ?? "not_paid";
                string?  oldDeliveryStatus = reader["delivery_status"]   as string ?? "to_plan";
                decimal? oldPrice          = reader["price"] == DBNull.Value ? null : (decimal?)reader["price"];
                int      oldTuning         = reader["tuning_sessions_agreed"] == DBNull.Value ? 0 : (int)reader["tuning_sessions_agreed"];
                int?     oldBenchModelId   = reader["bench_model_id"] == DBNull.Value ? null : (int?)reader["bench_model_id"];

                Diff(changes, "invoice_number",    oldInvoice,                body.InvoiceNumber);
                Diff(changes, "from_location",     oldFrom,                   body.FromLocation);
                Diff(changes, "old_piano_dest",    oldDest,                   body.OldPianoDest);
                Diff(changes, "surcharge_amount",  oldSurcharge.ToString("F2"), body.SurchargeAmount.ToString("F2"));
                Diff(changes, "cheque_to_collect", oldCheque.ToString(),      body.ChequeToCollect.ToString());
                Diff(changes, "google_review",     oldGoogle.ToString(),      body.GoogleReview.ToString());
                Diff(changes, "fully_paid",        oldPaid.ToString(),        body.FullyPaid.ToString());
                Diff(changes, "staff_notes",       oldNotes,                  body.StaffNotes);
                if (body.PianoSerial != null)
                    Diff(changes, "piano_serial",  oldSerial,                 body.PianoSerial);
                Diff(changes, "payment_status",    oldPaymentStatus,          body.PaymentStatus  ?? "not_paid");
                Diff(changes, "delivery_status",   oldDeliveryStatus,         body.DeliveryStatus ?? "to_plan");
                Diff(changes, "price",             oldPrice?.ToString("F2"),  body.Price?.ToString("F2"));
                Diff(changes, "tuning_sessions_agreed", oldTuning.ToString(), body.TuningSessionsAgreed.ToString());
                Diff(changes, "bench_model_id", oldBenchModelId?.ToString(), body.BenchModelId?.ToString());
            }

            // ── Update ───────────────────────────────────────────────
            var cmd = new SqlCommand(@"
                UPDATE dbo.Registrations SET
                    invoice_number    = @invoiceNumber,
                    from_location     = @fromLocation,
                    old_piano_dest    = @oldPianoDest,
                    surcharge_amount  = @surchargeAmount,
                    cheque_to_collect = @chequeToCollect,
                    google_review     = @googleReview,
                    fully_paid        = @fullyPaid,
                    staff_notes       = @staffNotes,
                    piano_serial      = COALESCE(@pianoSerial, piano_serial),
                    payment_status    = @paymentStatus,
                    delivery_status   = @deliveryStatus,
                    price                   = @price,
                    tuning_sessions_agreed  = @tuningSessionsAgreed,
                    bench_model_id          = @benchModelId
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id",               body.Id);
            cmd.Parameters.AddWithValue("@invoiceNumber",    (object?)body.InvoiceNumber   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@fromLocation",     (object?)body.FromLocation    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@oldPianoDest",     (object?)body.OldPianoDest    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@surchargeAmount",  body.SurchargeAmount);
            cmd.Parameters.AddWithValue("@chequeToCollect",  body.ChequeToCollect);
            cmd.Parameters.AddWithValue("@googleReview",     body.GoogleReview);
            cmd.Parameters.AddWithValue("@fullyPaid",        body.FullyPaid);
            cmd.Parameters.AddWithValue("@staffNotes",       (object?)body.StaffNotes      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pianoSerial",      (object?)body.PianoSerial     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@paymentStatus",    body.PaymentStatus  ?? "not_paid");
            cmd.Parameters.AddWithValue("@deliveryStatus",   body.DeliveryStatus ?? "to_plan");
            cmd.Parameters.AddWithValue("@price",                (object?)body.Price           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@tuningSessionsAgreed", body.TuningSessionsAgreed);
            cmd.Parameters.AddWithValue("@benchModelId",         (object?)body.BenchModelId ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            // ── Audit log ────────────────────────────────────────────
            if (changes.Count > 0)
                await WriteAuditLog(conn, body.Id.Value, changedBy, "staff", changes);

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateRegistration failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static void Diff(Dictionary<string, string?[]> changes, string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal)
            changes[field] = [oldVal, newVal];
    }

    internal static async Task WriteAuditLog(SqlConnection conn, int regId, string changedBy, string section, Dictionary<string, string?[]> changes)
    {
        var json = JsonSerializer.Serialize(changes.ToDictionary(
            kvp => kvp.Key,
            kvp => new { old = kvp.Value[0], @new = kvp.Value[1] }));

        var logCmd = new SqlCommand(@"
            INSERT INTO dbo.AuditLog (registration_id, changed_by, section, changes_json)
            VALUES (@regId, @changedBy, @section, @changes)", conn);
        logCmd.Parameters.AddWithValue("@regId",     regId);
        logCmd.Parameters.AddWithValue("@changedBy", changedBy);
        logCmd.Parameters.AddWithValue("@section",   section);
        logCmd.Parameters.AddWithValue("@changes",   json);
        await logCmd.ExecuteNonQueryAsync();
    }

    internal static string GetUsername(HttpRequest req)
    {
        var auth = req.Headers["Authorization"].ToString();
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

public class StaffUpdate
{
    public int?     Id              { get; set; }
    public string?  InvoiceNumber   { get; set; }
    public string?  FromLocation    { get; set; }
    public string?  OldPianoDest    { get; set; }
    public decimal  SurchargeAmount { get; set; }
    public bool     ChequeToCollect { get; set; }
    public bool     GoogleReview    { get; set; }
    public bool     FullyPaid       { get; set; }
    public string?  StaffNotes      { get; set; }
    public string?  PianoSerial     { get; set; }
    public string?  PaymentStatus   { get; set; }
    public string?  DeliveryStatus  { get; set; }
    public decimal? Price                  { get; set; }
    public int      TuningSessionsAgreed  { get; set; }
    public int?     BenchModelId          { get; set; }
}
