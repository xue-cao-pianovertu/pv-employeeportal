using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PV.AZFunction;

public class GetEventGuests
{
    private readonly ILogger<GetEventGuests> _logger;
    public GetEventGuests(ILogger<GetEventGuests> logger) => _logger = logger;

    [Function("GetEventGuests")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "admin" && role != "staff")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        var eventCode = req.Query["event_code"].ToString().Trim().ToUpperInvariant();
        var sqlConn   = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var sql = @"
                SELECT g.id, e.event_code, e.event_name,
                       g.first_name, g.last_name, g.email, g.phone, g.created_at
                FROM dbo.EventGuests g
                JOIN dbo.Events e ON e.id = g.event_id";

            if (!string.IsNullOrEmpty(eventCode))
                sql += " WHERE e.event_code = @code";

            sql += " ORDER BY g.created_at DESC";

            var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrEmpty(eventCode))
                cmd.Parameters.AddWithValue("@code", eventCode);

            var rows = new List<object>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new
                {
                    id         = reader.GetInt32(0),
                    event_code = reader.GetString(1),
                    event_name = reader.GetString(2),
                    first_name = reader.GetString(3),
                    last_name  = reader.GetString(4),
                    email      = reader.GetString(5),
                    phone      = reader.IsDBNull(6) ? null : reader.GetString(6),
                    created_at = reader.GetDateTime(7).ToString("yyyy-MM-dd HH:mm")
                });
            }

            return new OkObjectResult(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEventGuests failed");
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
