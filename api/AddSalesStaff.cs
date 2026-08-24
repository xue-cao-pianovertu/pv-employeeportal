using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace PV.AZFunction;

public class AddSalesStaff
{
    private readonly ILogger<AddSalesStaff> _logger;
    public AddSalesStaff(ILogger<AddSalesStaff> logger) => _logger = logger;

    [Function("AddSalesStaff")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "patch")] HttpRequest req)
    {
        if (!IsAdmin(req))
            return new UnauthorizedObjectResult(new { error = "Admin access required" });

        SalesStaffPayload? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<SalesStaffPayload>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            if (req.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                // Add a new sales staff member
                if (string.IsNullOrWhiteSpace(body?.Name))
                    return new BadRequestObjectResult(new { error = "name required" });

                var cmd = new SqlCommand(
                    "INSERT INTO dbo.SalesStaff (name) OUTPUT INSERTED.id VALUES (@name)", conn);
                cmd.Parameters.AddWithValue("@name", body.Name.Trim());
                var newId = (int)(await cmd.ExecuteScalarAsync() ?? 0);
                return new OkObjectResult(new { success = true, id = newId });
            }
            else
            {
                // Toggle is_active
                if (body?.Id == null)
                    return new BadRequestObjectResult(new { error = "id required" });

                var cmd = new SqlCommand(
                    "UPDATE dbo.SalesStaff SET is_active = @active WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id",     body.Id.Value);
                cmd.Parameters.AddWithValue("@active", body.IsActive ?? false);
                await cmd.ExecuteNonQueryAsync();
                return new OkObjectResult(new { success = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddSalesStaff failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private static bool IsAdmin(HttpRequest req)
    {
        var auth = req.Headers["X-Token"].ToString();
        if (!auth.StartsWith("Bearer ")) auth = req.Headers["Authorization"].ToString();
        if (!auth.StartsWith("Bearer ")) return false;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(auth["Bearer ".Length..].Trim());
            var role    = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "";
            return role == "admin";
        }
        catch { return false; }
    }
}

public class SalesStaffPayload
{
    public int?    Id       { get; set; }
    public string? Name     { get; set; }
    public bool?   IsActive { get; set; }
}
