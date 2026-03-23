using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PV.AZFunction;

public class UpdateLivraison
{
    private readonly ILogger<UpdateLivraison> _logger;
    public UpdateLivraison(ILogger<UpdateLivraison> logger) => _logger = logger;

    [Function("UpdateLivraison")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        LivraisonUpdate? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<LivraisonUpdate>(
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
                SELECT delivery_street, delivery_apt, delivery_city, delivery_province, delivery_postal,
                       within_40km, delivery_floor, delivery_elevator,
                       steps_outside, steps_inside, stair_turns,
                       crane_required, delivery_asap, delivery_date, delivery_notes, mover_notes,
                       collect_piano, collect_desc, recycle_piano, surcharge_flag
                FROM dbo.Registrations WHERE id = @id", conn))
            {
                selCmd.Parameters.AddWithValue("@id", body.Id);
                using var reader = await selCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return new NotFoundObjectResult(new { error = "Registration not found" });

                string? oldStreet      = reader["delivery_street"]   as string;
                string? oldApt         = reader["delivery_apt"]       as string;
                string? oldCity        = reader["delivery_city"]      as string;
                string? oldProv        = reader["delivery_province"]  as string;
                string? oldPostal      = reader["delivery_postal"]    as string;
                bool    oldIn40km      = reader["within_40km"]        != DBNull.Value && (bool)reader["within_40km"];
                string? oldFloor       = reader["delivery_floor"]     as string;
                bool    oldElev        = reader["delivery_elevator"]  != DBNull.Value && (bool)reader["delivery_elevator"];
                int     oldStepsOut    = reader["steps_outside"]      != DBNull.Value ? Convert.ToInt32(reader["steps_outside"]) : 0;
                int     oldStepsIn     = reader["steps_inside"]       != DBNull.Value ? Convert.ToInt32(reader["steps_inside"]) : 0;
                int     oldStairT      = reader["stair_turns"]        != DBNull.Value ? Convert.ToInt32(reader["stair_turns"]) : 0;
                bool    oldCrane       = reader["crane_required"]     != DBNull.Value && (bool)reader["crane_required"];
                bool    oldAsap        = reader["delivery_asap"]      != DBNull.Value && (bool)reader["delivery_asap"];
                string? oldDelivDate   = reader["delivery_date"]      == DBNull.Value ? null : ((DateTime)reader["delivery_date"]).ToString("yyyy-MM-dd");
                string? oldDelivNotes  = reader["delivery_notes"]     as string;
                string? oldMoverNotes  = reader["mover_notes"]        as string;
                bool    oldCollect     = reader["collect_piano"]      != DBNull.Value && (bool)reader["collect_piano"];
                string? oldCollectDesc = reader["collect_desc"]       as string;
                bool    oldRecycle     = reader["recycle_piano"]      != DBNull.Value && (bool)reader["recycle_piano"];
                bool    oldSurchFlag   = reader["surcharge_flag"]     != DBNull.Value && (bool)reader["surcharge_flag"];

                Diff(changes, "delivery_street",   oldStreet,               body.DeliveryStreet);
                Diff(changes, "delivery_apt",       oldApt,                  body.DeliveryApt);
                Diff(changes, "delivery_city",      oldCity,                 body.DeliveryCity);
                Diff(changes, "delivery_province",  oldProv,                 body.DeliveryProvince);
                Diff(changes, "delivery_postal",    oldPostal,               body.DeliveryPostal);
                Diff(changes, "within_40km",        oldIn40km.ToString(),    body.Within40km.ToString());
                Diff(changes, "delivery_floor",     oldFloor,                body.DeliveryFloor);
                Diff(changes, "delivery_elevator",  oldElev.ToString(),      body.DeliveryElevator.ToString());
                Diff(changes, "steps_outside",      oldStepsOut.ToString(),  body.StepsOutside.ToString());
                Diff(changes, "steps_inside",       oldStepsIn.ToString(),   body.StepsInside.ToString());
                Diff(changes, "stair_turns",        oldStairT.ToString(),    body.StairTurns.ToString());
                Diff(changes, "crane_required",     oldCrane.ToString(),     body.CraneRequired.ToString());
                Diff(changes, "delivery_asap",      oldAsap.ToString(),      body.DeliveryAsap.ToString());
                Diff(changes, "delivery_date",      oldDelivDate,            body.DeliveryDate);
                Diff(changes, "delivery_notes",     oldDelivNotes,           body.DeliveryNotes);
                Diff(changes, "mover_notes",        oldMoverNotes,           body.MoverNotes);
                Diff(changes, "collect_piano",      oldCollect.ToString(),   body.CollectPiano.ToString());
                Diff(changes, "collect_desc",       oldCollectDesc,          body.CollectDesc);
                Diff(changes, "recycle_piano",      oldRecycle.ToString(),   body.RecyclePiano.ToString());
                Diff(changes, "surcharge_flag",     oldSurchFlag.ToString(), body.SurchargeFlag.ToString());
            }

            // ── Parse delivery date ────────────────────────────────────
            DateTime? newDelivDate = null;
            if (body.DeliveryDate != null && DateTime.TryParse(body.DeliveryDate, out var pd))
                newDelivDate = pd;

            // ── Update ───────────────────────────────────────────────
            var cmd = new SqlCommand(@"
                UPDATE dbo.Registrations SET
                    delivery_street    = @street,
                    delivery_apt       = @apt,
                    delivery_city      = @city,
                    delivery_province  = @province,
                    delivery_postal    = @postal,
                    within_40km        = @within40km,
                    delivery_floor     = @floor,
                    delivery_elevator  = @elevator,
                    steps_outside      = @stepsOut,
                    steps_inside       = @stepsIn,
                    stair_turns        = @stairT,
                    crane_required     = @crane,
                    delivery_asap      = @asap,
                    delivery_date      = @delivDate,
                    delivery_notes     = @delivNotes,
                    mover_notes        = @moverNotes,
                    collect_piano      = @collectPiano,
                    collect_desc       = @collectDesc,
                    recycle_piano      = @recyclePiano,
                    surcharge_flag     = @surchFlag
                WHERE id = @id", conn);

            cmd.Parameters.AddWithValue("@id",           body.Id);
            cmd.Parameters.AddWithValue("@street",       (object?)body.DeliveryStreet    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@apt",          (object?)body.DeliveryApt       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@city",         (object?)body.DeliveryCity      ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@province",     (object?)body.DeliveryProvince  ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@postal",       (object?)body.DeliveryPostal    ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@within40km",   body.Within40km);
            cmd.Parameters.AddWithValue("@floor",        (object?)body.DeliveryFloor     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@elevator",     body.DeliveryElevator);
            cmd.Parameters.AddWithValue("@stepsOut",     body.StepsOutside);
            cmd.Parameters.AddWithValue("@stepsIn",      body.StepsInside);
            cmd.Parameters.AddWithValue("@stairT",       body.StairTurns);
            cmd.Parameters.AddWithValue("@crane",        body.CraneRequired);
            cmd.Parameters.AddWithValue("@asap",         body.DeliveryAsap);
            cmd.Parameters.AddWithValue("@delivDate",    (object?)newDelivDate           ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@delivNotes",   (object?)body.DeliveryNotes     ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@moverNotes",   (object?)body.MoverNotes        ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@collectPiano", body.CollectPiano);
            cmd.Parameters.AddWithValue("@collectDesc",  (object?)body.CollectDesc       ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@recyclePiano", body.RecyclePiano);
            cmd.Parameters.AddWithValue("@surchFlag",    body.SurchargeFlag);

            await cmd.ExecuteNonQueryAsync();

            if (changes.Count > 0)
                await UpdateRegistration.WriteAuditLog(conn, body.Id.Value, changedBy, "livraison", changes);

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateLivraison failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static void Diff(Dictionary<string, string?[]> changes, string field, string? oldVal, string? newVal)
    {
        if (oldVal != newVal) changes[field] = [oldVal, newVal];
    }
}

public class LivraisonUpdate
{
    public int?     Id                { get; set; }
    public string?  DeliveryStreet    { get; set; }
    public string?  DeliveryApt       { get; set; }
    public string?  DeliveryCity      { get; set; }
    public string?  DeliveryProvince  { get; set; }
    public string?  DeliveryPostal    { get; set; }
    public bool     Within40km        { get; set; }
    public string?  DeliveryFloor     { get; set; }
    public bool     DeliveryElevator  { get; set; }
    public int      StepsOutside      { get; set; }
    public int      StepsInside       { get; set; }
    public int      StairTurns        { get; set; }
    public bool     CraneRequired     { get; set; }
    public bool     DeliveryAsap      { get; set; }
    public string?  DeliveryDate      { get; set; }
    public string?  DeliveryNotes     { get; set; }
    public string?  MoverNotes        { get; set; }
    public bool     CollectPiano      { get; set; }
    public string?  CollectDesc       { get; set; }
    public bool     RecyclePiano      { get; set; }
    public bool     SurchargeFlag     { get; set; }
}
