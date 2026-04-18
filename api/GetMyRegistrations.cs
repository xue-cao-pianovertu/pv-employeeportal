using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;

namespace PV.AZFunction;

public class GetMyRegistrations
{
    private readonly ILogger<GetMyRegistrations> _logger;
    public GetMyRegistrations(ILogger<GetMyRegistrations> logger) => _logger = logger;

    [Function("GetMyRegistrations")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var userId = GetUserId(req);
        if (userId == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT
                    r.id, r.ref_id, r.language,
                    r.customer_last_name, r.customer_first_name, r.customer_email,
                    r.customer_phone1, r.customer_phone2,
                    r.delivery_street, r.delivery_apt, r.delivery_city,
                    r.delivery_province, r.delivery_postal,
                    r.within_40km, r.delivery_floor, r.delivery_elevator,
                    r.steps_outside, r.steps_inside, r.stair_turns, r.mover_notes,
                    r.collect_piano, r.collect_desc, r.recycle_piano, r.crane_required,
                    r.delivery_asap, r.delivery_date, r.delivery_notes, r.surcharge_flag,
                    r.piano_make, r.piano_model, r.piano_serial, r.piano_color,
                    r.purchase_date, r.accessories, r.bench_notes, r.piano_notes,
                    r.humidity_confirmed,
                    r.warranty_pdf_blob, r.tradeup_pdf_blob,
                    r.signature_type, r.signature_blob_name, r.signed_at,
                    r.status, r.created_at,
                    cat.name_fr     AS category_name_fr,
                    cat.name_en     AS category_name_en,
                    cat.name_zh     AS category_name_zh,
                    cat.has_warranty  AS category_has_warranty,
                    cat.has_tradeup   AS category_has_tradeup,
                    pt.name_fr   AS type_name_fr,
                    pt.name_en   AS type_name_en,
                    pt.name_zh   AS type_name_zh,
                    pt.brand_name AS type_brand,
                    b.name       AS bench_name
                FROM dbo.Registrations r
                LEFT JOIN dbo.PianoCategory cat ON cat.id = r.piano_category_id
                LEFT JOIN dbo.PianoType    pt  ON pt.id  = r.piano_type_id
                LEFT JOIN dbo.bench        b   ON b.id   = r.bench_model_id
                INNER JOIN dbo.Users u ON u.username = r.customer_email
                WHERE u.id = @userId
                ORDER BY r.created_at DESC", conn);

            cmd.Parameters.AddWithValue("@userId", userId.Value);

            var rows = new List<Dictionary<string, object?>>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            return new OkObjectResult(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyRegistrations failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static int? GetUserId(HttpRequest req)
    {
        var auth = req.Headers["X-Token"].ToString();
        if (string.IsNullOrEmpty(auth)) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer ")) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            var sub     = jwt.Payload.Sub;
            return sub != null && int.TryParse(sub, out var id) ? id : null;
        }
        catch { return null; }
    }
}
