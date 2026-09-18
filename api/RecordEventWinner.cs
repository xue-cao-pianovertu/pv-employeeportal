using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace PV.AZFunction;

public class RecordEventWinner
{
    private readonly ILogger<RecordEventWinner> _logger;
    public RecordEventWinner(ILogger<RecordEventWinner> logger) => _logger = logger;

    [Function("RecordEventWinner")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "admin" && role != "staff")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        RecordWinnerPayload? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<RecordWinnerPayload>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (body == null || string.IsNullOrWhiteSpace(body.EventCode))
            return new BadRequestObjectResult(new { error = "event_code required" });
        if (body.GuestId <= 0)
            return new BadRequestObjectResult(new { error = "guest_id required" });
        if (body.DrawNumber <= 0)
            return new BadRequestObjectResult(new { error = "draw_number required" });

        var drawnBy = GetUsername(req);
        var code    = body.EventCode.Trim().ToUpperInvariant();
        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // Resolve event_code → event_id
            var lookupCmd = new SqlCommand(
                "SELECT id FROM dbo.Events WHERE event_code = @code", conn);
            lookupCmd.Parameters.AddWithValue("@code", code);
            var eventIdObj = await lookupCmd.ExecuteScalarAsync();
            if (eventIdObj == null)
                return new NotFoundObjectResult(new { error = "event not found" });

            var eventId = (int)eventIdObj;

            var insertCmd = new SqlCommand(@"
                INSERT INTO dbo.EventDraws (event_id, winner_guest_id, draw_number, drawn_by)
                OUTPUT INSERTED.id
                VALUES (@eventId, @guestId, @drawNumber, @drawnBy)", conn);
            insertCmd.Parameters.AddWithValue("@eventId",    eventId);
            insertCmd.Parameters.AddWithValue("@guestId",    body.GuestId);
            insertCmd.Parameters.AddWithValue("@drawNumber", body.DrawNumber);
            insertCmd.Parameters.AddWithValue("@drawnBy",    drawnBy ?? (object)DBNull.Value);

            var drawId = (int)(await insertCmd.ExecuteScalarAsync())!;
            return new OkObjectResult(new { success = true, draw_id = drawId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecordEventWinner failed");
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

    private static string? GetUsername(HttpRequest req)
    {
        var auth = req.Headers["X-Token"].ToString();
        if (!auth.StartsWith("Bearer ")) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer ")) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            return jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value
                ?? jwt.Payload.Sub;
        }
        catch { return null; }
    }
}

public class RecordWinnerPayload
{
    public string? EventCode  { get; set; }
    public int     GuestId    { get; set; }
    public int     DrawNumber { get; set; }
}
