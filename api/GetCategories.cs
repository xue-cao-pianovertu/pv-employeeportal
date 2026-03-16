using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace PV.AZFunction;

public class GetCategories
{
    private readonly ILogger<GetCategories> _logger;

    public GetCategories(ILogger<GetCategories> logger)
    {
        _logger = logger;
    }

    [Function("GetCategories")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("GetCategories triggered.");

        var connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        var categories = new List<object>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var cmd = new SqlCommand(@"
            SELECT id, name_fr, name_en, name_zh, 
                   allows_manual_entry, has_warranty 
            FROM PianoCategory 
            ORDER BY id", conn);

        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            categories.Add(new
            {
                id                 = reader.GetInt32(0),
                name_fr            = reader.GetString(1),
                name_en            = reader.GetString(2),
                name_zh            = reader.GetString(3),
                allows_manual_entry = reader.GetBoolean(4),
                has_warranty       = reader.GetBoolean(5)
            });
        }

        return new OkObjectResult(categories);
    }
}
