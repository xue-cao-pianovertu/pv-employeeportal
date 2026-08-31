using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PV.AZFunction;

public class DeleteAcademyRegistration
{
    private readonly ILogger<DeleteAcademyRegistration> _logger;
    public DeleteAcademyRegistration(ILogger<DeleteAcademyRegistration> logger) => _logger = logger;

    [Function("DeleteAcademyRegistration")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete")] HttpRequest req)
    {
        var role = GetRole(req);
        if (role == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "apvadmin")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        JsonElement body;
        try
        {
            using var doc = await JsonDocument.ParseAsync(req.Body);
            body = doc.RootElement.Clone();
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Corps JSON invalide" });
        }

        if (!body.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out var id))
            return new BadRequestObjectResult(new { error = "id requis" });

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // Fetch student_user_id before deleting the row
            int? studentUserId = null;
            var getCmd = new SqlCommand(
                "SELECT student_user_id FROM dbo.AcademyRegistrations WHERE id = @id", conn);
            getCmd.Parameters.AddWithValue("@id", id);
            var scalar = await getCmd.ExecuteScalarAsync();
            if (scalar != null && scalar != DBNull.Value)
                studentUserId = (int)scalar;

            // Delete audit log first (FK: AcademyAuditLog.registration_id → AcademyRegistrations.id)
            var delAudit = new SqlCommand(
                "DELETE FROM dbo.AcademyAuditLog WHERE registration_id = @id", conn);
            delAudit.Parameters.AddWithValue("@id", id);
            await delAudit.ExecuteNonQueryAsync();

            // Delete the registration
            var delReg = new SqlCommand(
                "DELETE FROM dbo.AcademyRegistrations WHERE id = @id", conn);
            delReg.Parameters.AddWithValue("@id", id);
            await delReg.ExecuteNonQueryAsync();

            // Delete the student user account (guard with role check to avoid deleting non-students)
            if (studentUserId.HasValue)
            {
                var delUser = new SqlCommand(
                    "DELETE FROM dbo.Users WHERE id = @userId AND role = 'student'", conn);
                delUser.Parameters.AddWithValue("@userId", studentUserId.Value);
                await delUser.ExecuteNonQueryAsync();
            }

            _logger.LogInformation("Academy registration {Id} deleted", id);
            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteAcademyRegistration failed");
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
