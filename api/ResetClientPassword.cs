using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace PV.AZFunction;

public class ResetClientPassword
{
    private readonly ILogger<ResetClientPassword> _logger;
    public ResetClientPassword(ILogger<ResetClientPassword> logger) => _logger = logger;

    [Function("ResetClientPassword")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "admin" && role != "staff")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        ResetPasswordBody? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ResetPasswordBody>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (string.IsNullOrWhiteSpace(body?.CustomerEmail))
            return new BadRequestObjectResult(new { error = "customer_email required" });

        var newPassword = GeneratePassword(8);

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // Verify user exists
            var checkCmd = new SqlCommand(
                "SELECT id FROM dbo.Users WHERE username = @email", conn);
            checkCmd.Parameters.AddWithValue("@email", body.CustomerEmail);
            var userId = await checkCmd.ExecuteScalarAsync();

            if (userId == null)
                return new NotFoundObjectResult(new { error = "Utilisateur introuvable" });

            var updateCmd = new SqlCommand(
                "UPDATE dbo.Users SET password = @pwd WHERE id = @id", conn);
            updateCmd.Parameters.AddWithValue("@pwd", newPassword);
            updateCmd.Parameters.AddWithValue("@id", userId);
            await updateCmd.ExecuteNonQueryAsync();

            return new OkObjectResult(new { success = true, new_password = newPassword });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ResetClientPassword failed");
            return new ObjectResult(new { error = "Erreur serveur" }) { StatusCode = 500 };
        }
    }

    private static string? GetRole(HttpRequest req)
    {
        var auth = req.Headers["X-Token"].ToString();
        if (!auth.StartsWith("Bearer ")) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer ")) return null;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            return jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
        }
        catch { return null; }
    }

    private static string GeneratePassword(int length)
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }
}

public class ResetPasswordBody
{
    public string? CustomerEmail { get; set; }
}
