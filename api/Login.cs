using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace PV.AZFunction;

public class Login
{
    private readonly ILogger<Login> _logger;

    public Login(ILogger<Login> logger)
    {
        _logger = logger;
    }

    [Function("Login")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req)
    {
        _logger.LogInformation("Login triggered.");

        LoginRequest? body;
        try
        {
            body = await JsonSerializer.DeserializeAsync<LoginRequest>(
                req.Body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return new BadRequestObjectResult(new { error = "Invalid JSON" });
        }

        if (string.IsNullOrWhiteSpace(body?.Username) || string.IsNullOrWhiteSpace(body?.Password))
            return new BadRequestObjectResult(new { error = "Username and password required" });

        var sqlConn   = Environment.GetEnvironmentVariable("SqlConnectionString");
        var jwtSecret = Environment.GetEnvironmentVariable("JwtSecret");

        if (string.IsNullOrEmpty(jwtSecret))
        {
            _logger.LogError("JwtSecret not configured");
            return new ObjectResult(new { error = "Server configuration error" }) { StatusCode = 500 };
        }

        try
        {
            using var conn = new SqlConnection(sqlConn);
            await conn.OpenAsync();

            var cmd = new SqlCommand(
                "SELECT id, full_name, role FROM dbo.Users " +
                "WHERE username = @username AND password = @password AND is_active = 1", conn);
            cmd.Parameters.AddWithValue("@username", body.Username);
            cmd.Parameters.AddWithValue("@password", body.Password);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return new UnauthorizedObjectResult(new { error = "Invalid credentials" });

            var userId   = reader.GetInt32(0);
            var fullName = reader.IsDBNull(1) ? body.Username : reader.GetString(1);
            var role     = reader.GetString(2);

            var token = GenerateJwt(userId, body.Username, fullName, role, jwtSecret);

            _logger.LogInformation("Login success: {Username} ({Role})", body.Username, role);
            return new OkObjectResult(new { token, role, full_name = fullName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return new ObjectResult(new { error = "Server error" }) { StatusCode = 500 };
        }
    }

    private static string GenerateJwt(int userId, string username, string fullName, string role, string secret)
    {
        var key     = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds   = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(8);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,             userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName,      username),
            new Claim("full_name",                             fullName),
            new Claim(ClaimTypes.Role,                         role),
            new Claim(JwtRegisteredClaimNames.Jti,             Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             "pianovertu",
            audience:           "pianovertu",
            claims:             claims,
            expires:            expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}
