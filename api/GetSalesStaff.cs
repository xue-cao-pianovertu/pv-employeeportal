using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace PV.AZFunction;

public class GetSalesStaff
{
    private readonly ILogger<GetSalesStaff> _logger;
    public GetSalesStaff(ILogger<GetSalesStaff> logger) => _logger = logger;

    [Function("GetSalesStaff")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        var list = new List<object>();

        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(
                "SELECT id, name FROM dbo.SalesStaff WHERE is_active = 1 ORDER BY name", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new
                {
                    id   = reader.GetInt32(0),
                    name = reader.GetString(1)
                });
            }

            return new OkObjectResult(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSalesStaff failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}
