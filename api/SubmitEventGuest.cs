using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PV.AZFunction;

public class SubmitEventGuest
{
    private readonly ILogger<SubmitEventGuest> _logger;
    public SubmitEventGuest(ILogger<SubmitEventGuest> logger) => _logger = logger;

    [Function("SubmitEventGuest")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        EventGuestPayload? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<EventGuestPayload>(
                req.Body,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (body == null || string.IsNullOrWhiteSpace(body.EventCode))
            return new BadRequestObjectResult(new { error = "event_code required" });
        if (string.IsNullOrWhiteSpace(body.FirstName))
            return new BadRequestObjectResult(new { error = "first_name required" });
        if (string.IsNullOrWhiteSpace(body.LastName))
            return new BadRequestObjectResult(new { error = "last_name required" });
        if (string.IsNullOrWhiteSpace(body.Email) || !body.Email.Contains('@'))
            return new BadRequestObjectResult(new { error = "valid email required" });

        var code    = body.EventCode.Trim().ToUpperInvariant();
        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            // Resolve event_code → event_id
            var lookupCmd = new SqlCommand(
                "SELECT id FROM dbo.Events WHERE event_code = @code AND is_active = 1", conn);
            lookupCmd.Parameters.AddWithValue("@code", code);
            var eventIdObj = await lookupCmd.ExecuteScalarAsync();
            if (eventIdObj == null)
                return new NotFoundObjectResult(new { error = "not_found" });

            var eventId = (int)eventIdObj;

            var insertCmd = new SqlCommand(@"
                INSERT INTO dbo.EventGuests (event_id, first_name, last_name, email, phone)
                VALUES (@eventId, @firstName, @lastName, @email, @phone)", conn);
            insertCmd.Parameters.AddWithValue("@eventId",   eventId);
            insertCmd.Parameters.AddWithValue("@firstName", body.FirstName.Trim());
            insertCmd.Parameters.AddWithValue("@lastName",  body.LastName.Trim());
            insertCmd.Parameters.AddWithValue("@email",     body.Email.Trim().ToLowerInvariant());
            insertCmd.Parameters.AddWithValue("@phone",
                string.IsNullOrWhiteSpace(body.Phone) ? (object)DBNull.Value : body.Phone.Trim());

            await insertCmd.ExecuteNonQueryAsync();
            return new OkObjectResult(new { success = true });
        }
        catch (SqlException ex) when (ex.Number == 2627)
        {
            return new ObjectResult(new { duplicate = true }) { StatusCode = 409 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitEventGuest failed");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}

public class EventGuestPayload
{
    public string?  EventCode { get; set; }
    public string?  FirstName { get; set; }
    public string?  LastName  { get; set; }
    public string?  Email     { get; set; }
    public string?  Phone     { get; set; }
}
