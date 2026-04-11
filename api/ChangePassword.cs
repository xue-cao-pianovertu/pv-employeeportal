using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace PV.AZFunction;

public class ChangePassword
{
    private readonly ILogger<ChangePassword> _logger;
    public ChangePassword(ILogger<ChangePassword> logger) => _logger = logger;

    [Function("ChangePassword")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch")] HttpRequest req)
    {
        var userId = GetUserId(req);
        if (userId == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });

        ChangePasswordBody? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<ChangePasswordBody>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (string.IsNullOrWhiteSpace(body?.CurrentPassword) || string.IsNullOrWhiteSpace(body?.NewPassword))
            return new BadRequestObjectResult(new { error = "current_password and new_password required" });

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // Verify current password
            var checkCmd = new SqlCommand(
                "SELECT password FROM dbo.Users WHERE id = @id", conn);
            checkCmd.Parameters.AddWithValue("@id", userId.Value);
            var stored = (string?)await checkCmd.ExecuteScalarAsync();

            if (stored == null)
                return new UnauthorizedObjectResult(new { error = "Utilisateur introuvable" });

            if (stored != body.CurrentPassword)
                return new ObjectResult(new { error = "Mot de passe actuel incorrect" }) { StatusCode = 403 };

            // Update password
            var updateCmd = new SqlCommand(
                "UPDATE dbo.Users SET password = @pwd WHERE id = @id", conn);
            updateCmd.Parameters.AddWithValue("@pwd", body.NewPassword);
            updateCmd.Parameters.AddWithValue("@id", userId.Value);
            await updateCmd.ExecuteNonQueryAsync();

            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChangePassword failed");
            return new ObjectResult(new { error = "Erreur serveur" }) { StatusCode = 500 };
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

public class ChangePasswordBody
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword     { get; set; }
}
