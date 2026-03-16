using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace PV.AZFunction;

public class GetBenches
{
    private readonly ILogger<GetBenches> _logger;

    public GetBenches(ILogger<GetBenches> logger)
    {
        _logger = logger;
    }

    [Function("GetBenches")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("GetBenches triggered.");

        var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        var benches = new List<object>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand("SELECT id, name, description FROM bench ORDER BY id", conn);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            benches.Add(new
            {
                id          = reader.GetInt32(0),
                name        = reader.GetString(1),
                description = reader.IsDBNull(2) ? null : reader.GetString(2)
            });
        }

        return new OkObjectResult(benches);
    }
}