using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PV.AZFunction;

public class GetEvents
{
    private readonly ILogger<GetEvents> _logger;
    public GetEvents(ILogger<GetEvents> logger) => _logger = logger;

    [Function("GetEvents")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "admin" && role != "staff")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT
                    e.id, e.event_code, e.event_name, e.event_date,
                    e.promo_text, e.draw_count, e.prize_description,
                    e.is_active, e.created_at,
                    (SELECT COUNT(*) FROM dbo.EventGuests g WHERE g.event_id = e.id) AS guest_count,
                    (SELECT COUNT(*) FROM dbo.EventDraws  d WHERE d.event_id = e.id) AS draws_completed
                FROM dbo.Events e
                ORDER BY e.created_at DESC", conn);

            var rows = new List<object>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new
                {
                    id               = reader.GetInt32(0),
                    event_code       = reader.GetString(1),
                    event_name       = reader.GetString(2),
                    event_date       = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                    promo_text       = reader.IsDBNull(4) ? null : reader.GetString(4),
                    draw_count       = reader.GetInt32(5),
                    prize_description= reader.IsDBNull(6) ? null : reader.GetString(6),
                    is_active        = reader.GetBoolean(7),
                    created_at       = reader.GetDateTime(8).ToString("yyyy-MM-dd"),
                    guest_count      = reader.GetInt32(9),
                    draws_completed  = reader.GetInt32(10)
                });
            }

            return new OkObjectResult(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEvents failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
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
}
