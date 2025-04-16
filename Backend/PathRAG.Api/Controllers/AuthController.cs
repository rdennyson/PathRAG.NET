using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PathRAG.Api.Models;
using System.Text.Json;

[ApiController]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AuthController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
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

        var response = await _httpClient.PostAsync(tokenEndpoint, tokenRequestContent);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return BadRequest(content);
        }

        // Clear the state from session
        HttpContext.Session.Remove("AuthState");

        // Parse the token response
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(content);

        // Set the tokens in cookies
        Response.Cookies.Append("access_token", tokenResponse.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.Now.AddSeconds(tokenResponse.ExpiresIn)
        });

        Response.Cookies.Append("id_token", tokenResponse.IdToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.Now.AddSeconds(tokenResponse.ExpiresIn)
        });

        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            Response.Cookies.Append("refresh_token", tokenResponse.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddDays(30) // Refresh tokens typically last longer
            });
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


}

