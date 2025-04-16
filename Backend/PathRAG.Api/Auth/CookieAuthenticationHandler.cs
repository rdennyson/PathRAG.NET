using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace PathRAG.Api.Auth
{
    public class CookieAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public CookieAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                // Check if the id_token cookie exists
                if (!Request.Cookies.TryGetValue("id_token", out var idToken))
                {
                    return Task.FromResult(AuthenticateResult.NoResult());
                }

                // Parse the JWT token
                var handler = new JwtSecurityTokenHandler();
                var token = handler.ReadJwtToken(idToken);

                // Extract claims from the token
                var claims = new List<Claim>();

                // Add standard claims
                var nameIdentifier = token.Claims.FirstOrDefault(c => c.Type == "oid" || c.Type == "sub")?.Value;
                var name = token.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
                var email = token.Claims.FirstOrDefault(c => c.Type == "email" || c.Type == "upn")?.Value;

                if (!string.IsNullOrEmpty(nameIdentifier))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
                }

                if (!string.IsNullOrEmpty(name))
                {
                    claims.Add(new Claim(ClaimTypes.Name, name));
                }

                if (!string.IsNullOrEmpty(email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, email));
                }

                // If we couldn't extract the required claims, use default values
                if (!claims.Any(c => c.Type == ClaimTypes.NameIdentifier))
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, "default-user-id"));
                }

                if (!claims.Any(c => c.Type == ClaimTypes.Name))
                {
                    claims.Add(new Claim(ClaimTypes.Name, "Default User"));
                }

                if (!claims.Any(c => c.Type == ClaimTypes.Email))
                {
                    claims.Add(new Claim(ClaimTypes.Email, "user@example.com"));
                }

                var identity = new ClaimsIdentity(claims, Scheme.Name);
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, Scheme.Name);

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error authenticating user from cookies");
                return Task.FromResult(AuthenticateResult.Fail("Authentication failed"));
            }
        }
    }
}
