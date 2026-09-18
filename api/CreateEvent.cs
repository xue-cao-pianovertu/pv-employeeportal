using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PV.AZFunction;

public class CreateEvent
{
    private readonly ILogger<CreateEvent> _logger;
    public CreateEvent(ILogger<CreateEvent> logger) => _logger = logger;

    [Function("CreateEvent")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "admin")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        CreateEventPayload? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<CreateEventPayload>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (body == null || string.IsNullOrWhiteSpace(body.EventCode))
            return new BadRequestObjectResult(new { error = "event_code required" });
        if (string.IsNullOrWhiteSpace(body.EventName))
            return new BadRequestObjectResult(new { error = "event_name required" });

        var code = body.EventCode.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(code, @"^[A-Z0-9_\-]{1,30}$"))
            return new BadRequestObjectResult(new { error = "event_code must be 1-30 chars: A-Z 0-9 _ -" });

        var drawCount = body.DrawCount.GetValueOrDefault(1);
        if (drawCount < 1) drawCount = 1;

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                INSERT INTO dbo.Events (event_code, event_name, event_date, promo_text, draw_count, prize_description)
                OUTPUT INSERTED.id
                VALUES (@code, @name, @date, @promo, @drawCount, @prize)", conn);
            cmd.Parameters.AddWithValue("@code",      code);
            cmd.Parameters.AddWithValue("@name",      body.EventName.Trim());
            cmd.Parameters.AddWithValue("@date",
                string.IsNullOrWhiteSpace(body.EventDate) ? (object)DBNull.Value : body.EventDate.Trim());
            cmd.Parameters.AddWithValue("@promo",
                string.IsNullOrWhiteSpace(body.PromoText) ? (object)DBNull.Value : body.PromoText.Trim());
            cmd.Parameters.AddWithValue("@drawCount", drawCount);
            cmd.Parameters.AddWithValue("@prize",
                string.IsNullOrWhiteSpace(body.PrizeDescription) ? (object)DBNull.Value : body.PrizeDescription.Trim());

            var newId = (int)(await cmd.ExecuteScalarAsync())!;
            return new OkObjectResult(new { success = true, id = newId });
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return new ObjectResult(new { error = "code_taken" }) { StatusCode = 409 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateEvent failed");
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

public class CreateEventPayload
{
    public string?  EventCode        { get; set; }
    public string?  EventName        { get; set; }
    public string?  EventDate        { get; set; }
    public string?  PromoText        { get; set; }
    public int?     DrawCount        { get; set; }
    public string?  PrizeDescription { get; set; }
}
