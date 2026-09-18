using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace PV.AZFunction;

public class GetEvent
{
    private readonly ILogger<GetEvent> _logger;
    public GetEvent(ILogger<GetEvent> logger) => _logger = logger;

    [Function("GetEvent")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var code = req.Query["code"].ToString().Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
            return new BadRequestObjectResult(new { error = "code required" });

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT id, event_code, event_name, event_date, promo_text
                FROM dbo.Events
                WHERE event_code = @code AND is_active = 1", conn);
            cmd.Parameters.AddWithValue("@code", code);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return new NotFoundObjectResult(new { error = "not_found" });

            return new OkObjectResult(new
            {
                id          = reader.GetInt32(0),
                event_code  = reader.GetString(1),
                event_name  = reader.GetString(2),
                event_date  = reader.IsDBNull(3) ? null : reader.GetDateTime(3).ToString("yyyy-MM-dd"),
                promo_text  = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEvent failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}
