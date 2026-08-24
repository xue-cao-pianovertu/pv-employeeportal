using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace PV.AZFunction;

public class GetLeads
{
    private readonly ILogger<GetLeads> _logger;
    public GetLeads(ILogger<GetLeads> logger) => _logger = logger;

    [Function("GetLeads")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT
                    l.id, l.language,
                    l.first_name, l.last_name, l.email, l.phone,
                    l.contact_type, l.contact_date,
                    l.piano_interest, l.customer_notes,
                    l.lead_status, l.staff_notes,
                    l.created_at,
                    ls.label_fr AS status_label_fr,
                    ls.label_en AS status_label_en
                FROM dbo.Leads l
                LEFT JOIN dbo.LeadStatus ls ON ls.id = l.lead_status
                ORDER BY l.created_at DESC", conn);

            var rows = new List<Dictionary<string, object?>>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }

            return new OkObjectResult(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLeads failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}
