using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PV.AZFunction;

public class GetEventDraw
{
    private readonly ILogger<GetEventDraw> _logger;
    public GetEventDraw(ILogger<GetEventDraw> logger) => _logger = logger;

    [Function("GetEventDraw")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "admin" && role != "staff")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        var code = req.Query["code"].ToString().Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
            return new BadRequestObjectResult(new { error = "code required" });

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // Event info
            var evCmd = new SqlCommand(@"
                SELECT id, event_code, event_name, draw_count, prize_description
                FROM dbo.Events
                WHERE event_code = @code AND is_active = 1", conn);
            evCmd.Parameters.AddWithValue("@code", code);

            using var evReader = await evCmd.ExecuteReaderAsync();
            if (!await evReader.ReadAsync())
                return new NotFoundObjectResult(new { error = "not_found" });

            var eventId    = evReader.GetInt32(0);
            var eventCode  = evReader.GetString(1);
            var eventName  = evReader.GetString(2);
            var drawCount  = evReader.GetInt32(3);
            var prizeDesc  = evReader.IsDBNull(4) ? null : evReader.GetString(4);
            evReader.Close();

            // All guests
            var guestCmd = new SqlCommand(@"
                SELECT id, first_name, last_name
                FROM dbo.EventGuests
                WHERE event_id = @eventId
                ORDER BY id", conn);
            guestCmd.Parameters.AddWithValue("@eventId", eventId);

            var guests = new List<object>();
            using var gReader = await guestCmd.ExecuteReaderAsync();
            while (await gReader.ReadAsync())
            {
                guests.Add(new
                {
                    id         = gReader.GetInt32(0),
                    first_name = gReader.GetString(1),
                    last_name  = gReader.GetString(2)
                });
            }
            gReader.Close();

            // Past winners
            var winCmd = new SqlCommand(@"
                SELECT d.id, d.winner_guest_id, g.first_name, g.last_name,
                       d.draw_number, d.drawn_at
                FROM dbo.EventDraws d
                JOIN dbo.EventGuests g ON g.id = d.winner_guest_id
                WHERE d.event_id = @eventId
                ORDER BY d.draw_number", conn);
            winCmd.Parameters.AddWithValue("@eventId", eventId);

            var winners = new List<object>();
            using var wReader = await winCmd.ExecuteReaderAsync();
            while (await wReader.ReadAsync())
            {
                winners.Add(new
                {
                    id              = wReader.GetInt32(0),
                    winner_guest_id = wReader.GetInt32(1),
                    first_name      = wReader.GetString(2),
                    last_name       = wReader.GetString(3),
                    draw_number     = wReader.GetInt32(4),
                    drawn_at        = wReader.GetDateTime(5).ToString("yyyy-MM-dd HH:mm")
                });
            }

            return new OkObjectResult(new
            {
                event_info = new
                {
                    id                = eventId,
                    event_code        = eventCode,
                    event_name        = eventName,
                    draw_count        = drawCount,
                    prize_description = prizeDesc
                },
                guests,
                winners
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEventDraw failed");
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
