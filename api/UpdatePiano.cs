using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PV.AZFunction;

public class UpdatePiano
{
    private readonly ILogger<UpdatePiano> _logger;
    public UpdatePiano(ILogger<UpdatePiano> logger) => _logger = logger;

    [Function("UpdatePiano")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        PianoUpdate? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<PianoUpdate>(
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
                SELECT piano_make, piano_model, piano_color, purchase_date,
                       accessories, piano_notes, bench_notes
                FROM dbo.Registrations WHERE id = @id", conn))
            {
                selCmd.Parameters.AddWithValue("@id", body.Id);
                using var reader = await selCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return new NotFoundObjectResult(new { error = "Registration not found" });

                string? oldMake         = reader["piano_make"]    as string;
                string? oldModel        = reader["piano_model"]   as string;
                string? oldColor        = reader["piano_color"]   as string;
                string? oldPurchDate    = reader["purchase_date"] == DBNull.Value ? null : ((DateTime)reader["purchase_date"]).ToString("yyyy-MM-dd");
                string? oldAccessories  = reader["accessories"]   as string;
                string? oldPianoNotes   = reader["piano_notes"]   as string;
                string? oldBenchNotes   = reader["bench_notes"]   as string;

                Diff(changes, "piano_make",    oldMake,        body.PianoMake);
                Diff(changes, "piano_model",   oldModel,       body.PianoModel);
                Diff(changes, "piano_color",   oldColor,       body.PianoColor);
                Diff(changes, "purchase_date", oldPurchDate,   body.PurchaseDate);
                Diff(changes, "accessories",   oldAccessories, body.Accessories);
                Diff(changes, "piano_notes",   oldPianoNotes,  body.PianoNotes);
                Diff(changes, "bench_notes",   oldBenchNotes,  body.BenchNotes);
            }

            // ── Parse purchase date ────────────────────────────────────
            DateTime? newPurchDate = null;
            if (body.PurchaseDate != null && DateTime.TryParse(body.PurchaseDate, out var pd))
                newPurchDate = pd;

            // ── Update ───────────────────────────────────────────────
            var cmd = new SqlCommand(@"
                UPDATE dbo.Registrations SET
                    piano_make    = @pianoMake,
                    piano_model   = @pianoModel,
                    piano_color   = @pianoColor,
                    purchase_date = @purchaseDate,
                    accessories   = @accessories,
                    piano_notes   = @pianoNotes,
                    bench_notes   = @benchNotes
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id",           body.Id);
            cmd.Parameters.AddWithValue("@pianoMake",    (object?)body.PianoMake    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pianoModel",   (object?)body.PianoModel   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pianoColor",   (object?)body.PianoColor   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@purchaseDate", (object?)newPurchDate      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@accessories",  (object?)body.Accessories  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@pianoNotes",   (object?)body.PianoNotes   ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@benchNotes",   (object?)body.BenchNotes   ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();

            if (changes.Count > 0)
                await UpdateRegistration.WriteAuditLog(conn, body.Id.Value, changedBy, "piano", changes);

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdatePiano failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static void Diff(Dictionary<string, string?[]> changes, string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal) changes[field] = [oldVal, newVal];
    }
}

public class PianoUpdate
{
    public int?     Id            { get; set; }
    public string?  PianoMake     { get; set; }
    public string?  PianoModel    { get; set; }
    public string?  PianoColor    { get; set; }
    public string?  PurchaseDate  { get; set; }
    public string?  Accessories   { get; set; }
    public string?  PianoNotes    { get; set; }
    public string?  BenchNotes    { get; set; }
}
