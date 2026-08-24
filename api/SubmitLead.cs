using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace PV.AZFunction;

public class SubmitLead
{
    private readonly ILogger<SubmitLead> _logger;
    public SubmitLead(ILogger<SubmitLead> logger) => _logger = logger;

    [Function("SubmitLead")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        _logger.LogInformation("SubmitLead triggered.");

        LeadPayload? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<LeadPayload>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid JSON payload");
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (payload == null)
            return new BadRequestObjectResult(new { error = "Empty payload" });

        if (string.IsNullOrWhiteSpace(payload.FirstName))
            return new BadRequestObjectResult(new { error = "first_name required" });
        if (string.IsNullOrWhiteSpace(payload.LastName))
            return new BadRequestObjectResult(new { error = "last_name required" });
        if (string.IsNullOrWhiteSpace(payload.Email))
            return new BadRequestObjectResult(new { error = "email required" });
        var validContactTypes = new HashSet<string> { "visit", "call", "email", "web", "other" };
        if (payload.ContactType == null || !validContactTypes.Contains(payload.ContactType))
            return new BadRequestObjectResult(new { error = "contact_type must be visit, call, email, web, or other" });
        if (!DateOnly.TryParse(payload.ContactDate, out var contactDate))
            return new BadRequestObjectResult(new { error = "contact_date must be a valid date (YYYY-MM-DD)" });

        var lang = payload.Language?.ToLower() switch
        {
            "en" => "EN",
            "zh" => "ZH",
            _    => "FR"
        };

        var sqlConn = Environment.GetEnvironmentVariable("SqlConnectionString");
        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                INSERT INTO dbo.Leads
                    (language, first_name, last_name, email, phone,
                     contact_type, contact_date, piano_interest, customer_notes)
                VALUES
                    (@language, @firstName, @lastName, @email, @phone,
                     @contactType, @contactDate, @pianoInterest, @customerNotes)",
                conn);

            cmd.Parameters.AddWithValue("@language",      lang);
            cmd.Parameters.AddWithValue("@firstName",     payload.FirstName.Trim());
            cmd.Parameters.AddWithValue("@lastName",      payload.LastName.Trim());
            cmd.Parameters.AddWithValue("@email",         payload.Email.Trim());
            cmd.Parameters.AddWithValue("@phone",         (object?)payload.Phone?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@contactType",   payload.ContactType);
            cmd.Parameters.AddWithValue("@contactDate",   contactDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@pianoInterest", (object?)payload.PianoInterest?.Trim() ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@customerNotes", (object?)payload.CustomerNotes?.Trim() ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
            return new OkObjectResult(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SubmitLead DB error");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}

public class LeadPayload
{
    public string? Language      { get; set; }
    public string? FirstName     { get; set; }
    public string? LastName      { get; set; }
    public string? Email         { get; set; }
    public string? Phone         { get; set; }
    public string? ContactType   { get; set; }
    public string? ContactDate   { get; set; }
    public string? PianoInterest { get; set; }
    public string? CustomerNotes { get; set; }
}
