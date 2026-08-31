using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PV.AZFunction;

public class GetMyAcademyRegistration
{
    private readonly ILogger<GetMyAcademyRegistration> _logger;
    public GetMyAcademyRegistration(ILogger<GetMyAcademyRegistration> logger) => _logger = logger;

    [Function("GetMyAcademyRegistration")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var (userId, role) = GetUserInfo(req);
        if (userId == null)
            return new UnauthorizedObjectResult(new { error = "Token manquant ou invalide" });
        if (role != "student")
            return new ObjectResult(new { error = "Accès refusé" }) { StatusCode = 403 };

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(
                "SELECT * FROM dbo.AcademyRegistrations WHERE student_user_id = @userId", conn);
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

            if (rows.Count == 0)
                return new NotFoundObjectResult(new { error = "Aucune inscription trouvée" });

            return new OkObjectResult(rows[0]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyAcademyRegistration failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static (int? userId, string? role) GetUserInfo(HttpRequest req)
    {
        var auth = req.Headers["X-Token"].ToString();
        if (string.IsNullOrEmpty(auth)) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer ")) return (null, null);
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            var sub     = jwt.Payload.Sub;
            var id      = sub != null && int.TryParse(sub, out var i) ? i : (int?)null;
            var role    = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            return (id, role);
        }
        catch { return (null, null); }
    }
}
