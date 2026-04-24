using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace PV.AZFunction;

public class SetTestData
{
    private readonly ILogger<SetTestData> _logger;
    public SetTestData(ILogger<SetTestData> logger) => _logger = logger;

    [Function("SetTestData")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        // ── Auth: admin only ─────────────────────────────────────
        var auth = req.Headers["X-Token"].ToString();
        if (!auth.StartsWith("Bearer ")) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer "))
            return new ObjectResult(new { error = "Token manquant ou invalide" }) { StatusCode = 401 };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            var role    = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            if (role != "admin")
                return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };
        }
        catch
        {
            return new ObjectResult(new { error = "Token invalide" }) { StatusCode = 401 };
        }

        // ── Parse body ───────────────────────────────────────────
        SetTestDataBody? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SetTestDataBody>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (body?.Id == null)
            return new BadRequestObjectResult(new { error = "id required" });

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(
                "UPDATE dbo.Registrations SET test_data = @testData WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id",       body.Id);
            cmd.Parameters.AddWithValue("@testData", body.TestData ? 1 : 0);
            var rows = await cmd.ExecuteNonQueryAsync();

            if (rows == 0)
                return new NotFoundObjectResult(new { error = "Registration not found" });

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SetTestData failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}

public class SetTestDataBody
{
    public int?  Id       { get; set; }
    public bool  TestData { get; set; }
}
