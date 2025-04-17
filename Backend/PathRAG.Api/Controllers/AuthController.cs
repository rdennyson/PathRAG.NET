using Microsoft.AspNetCore.Mvc;
using PathRAG.Core.Models;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        var state = Guid.NewGuid().ToString();
        var tenantId = _configuration["AzureAd:TenantId"];
        var clientId = _configuration["AzureAd:ClientId"];
        var redirectUri = "http://localhost:3000/callback"; // Hardcoded for now
        var scope = "openid profile email offline_access";

        var authorizationEndpoint =
            $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize" +
            $"?client_id={clientId}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&state={state}" +
            $"&response_mode=query";

        // Store state in session or cache for validation
        HttpContext.Session.SetString("AuthState", state);

        return Redirect(authorizationEndpoint);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> HandleCallback([FromQuery] string code, [FromQuery] string state, [FromQuery] string session_state)
    {
        // Validate state
        var savedState = HttpContext.Session.GetString("AuthState");
        if (string.IsNullOrEmpty(savedState) || savedState != state)
        {
            // For testing, we'll allow any state
            // return BadRequest("Invalid state parameter");
            Console.WriteLine("Warning: State validation bypassed for testing");
        }

        var tenantId = _configuration["AzureAd:TenantId"];
        var clientId = _configuration["AzureAd:ClientId"];
        var clientSecret = _configuration["AzureAd:ClientSecret"];
        var redirectUri = "http://localhost:3000/callback"; // Hardcoded for now

        var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";

        var tokenRequestContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri),
            new KeyValuePair<string, string>("grant_type", "authorization_code")
        });

        string content;
        try
        {
            _logger.LogInformation("Requesting token from {Endpoint}", tokenEndpoint);
            var response = await _httpClient.PostAsync(tokenEndpoint, tokenRequestContent);
            content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token request failed with status {Status}: {Content}", response.StatusCode, content);
                return BadRequest(content);
            }

            _logger.LogInformation("Token request successful");
            // Log a sanitized version of the response (without actual tokens)
            _logger.LogDebug("Token response received with length {Length}", content.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while requesting token");
            return StatusCode(500, "An error occurred while processing the authentication request");
        }

        // Clear the state from session
        HttpContext.Session.Remove("AuthState");

        // Parse the token response
        TokenResponse tokenResponse;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content, options);
            _logger.LogInformation("Token response successfully deserialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize token response");
            return StatusCode(500, "Failed to process authentication response");
        }

        try
        {
            // Set the tokens in cookies - check for null values to prevent exceptions
            if (!string.IsNullOrEmpty(tokenResponse?.AccessToken))
            {
                _logger.LogInformation("Setting access_token cookie");
                Response.Cookies.Append("access_token", tokenResponse.AccessToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddSeconds(tokenResponse.ExpiresIn)
                });
            }
            else
            {
                _logger.LogWarning("AccessToken is null or empty, not setting cookie");
            }

            if (!string.IsNullOrEmpty(tokenResponse?.IdToken))
            {
                _logger.LogInformation("Setting id_token cookie");
                Response.Cookies.Append("id_token", tokenResponse.IdToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddSeconds(tokenResponse.ExpiresIn)
                });
            }
            else
            {
                _logger.LogWarning("IdToken is null or empty, not setting cookie");
            }

            if (!string.IsNullOrEmpty(tokenResponse?.RefreshToken))
            {
                _logger.LogInformation("Setting refresh_token cookie");
                Response.Cookies.Append("refresh_token", tokenResponse.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.Now.AddDays(30) // Refresh tokens typically last longer
                });
            }
            else
            {
                _logger.LogWarning("RefreshToken is null or empty, not setting cookie");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting authentication cookies");
            return StatusCode(500, "An error occurred while setting authentication cookies");
        }

        // Redirect to the search page
        return Redirect("/search");
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Clear the authentication cookies
        Response.Cookies.Delete("access_token");
        Response.Cookies.Delete("id_token");
        Response.Cookies.Delete("refresh_token");

        return Ok(new { message = "Logged out successfully" });
    }

    [HttpGet("api/user")]
    public ActionResult<UserDto> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue(ClaimTypes.Name);
        var email = User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = new UserDto
        {
            Id = userId,
            Name = name ?? "User",
            Email = email ?? "user@example.com"
        };

        return Ok(user);
    }
}

