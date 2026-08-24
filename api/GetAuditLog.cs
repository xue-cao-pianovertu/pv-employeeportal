using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace PV.AZFunction;

public class GetAuditLog
{
    private readonly ILogger<GetAuditLog> _logger;
    public GetAuditLog(ILogger<GetAuditLog> logger) => _logger = logger;

    [Function("GetAuditLog")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        bool isLead = int.TryParse(req.Query["lead_id"], out var leadId);
        bool isReg  = int.TryParse(req.Query["id"],      out var regId);

        if (!isLead && !isReg)
            return new BadRequestObjectResult(new { error = "id or lead_id parameter required" });

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            SqlCommand cmd;
            if (isLead)
            {
                cmd = new SqlCommand(@"
                    SELECT id, changed_by, changed_at, section, changes_json
                    FROM dbo.AuditLog
                    WHERE lead_id = @id
                    ORDER BY changed_at DESC", conn);
                cmd.Parameters.AddWithValue("@id", leadId);
            }
            else
            {
                cmd = new SqlCommand(@"
                    SELECT id, changed_by, changed_at, section, changes_json
                    FROM dbo.AuditLog
                    WHERE registration_id = @id
                    ORDER BY changed_at DESC", conn);
                cmd.Parameters.AddWithValue("@id", regId);
            }

            var rows = new List<Dictionary<string, object?>>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(new Dictionary<string, object?>
                {
                    ["id"]           = reader.GetInt32(0),
                    ["changed_by"]   = reader.GetString(1),
                    ["changed_at"]   = reader.GetDateTime(2),
                    ["section"]      = reader.GetString(3),
                    ["changes_json"] = reader.GetString(4),
                });
            }

            return new OkObjectResult(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAuditLog failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}
