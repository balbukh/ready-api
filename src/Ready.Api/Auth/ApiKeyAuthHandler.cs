using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ready.Infrastructure.Persistence;

namespace Ready.Api.Auth;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ReadyDbContext _db;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        ReadyDbContext db)
        : base(options, logger, encoder, clock)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var headerValues))
        {
            return AuthenticateResult.Fail("Missing X-Api-Key header");
        }

        var key = headerValues.FirstOrDefault();
        if (string.IsNullOrEmpty(key))
        {
            return AuthenticateResult.Fail("Empty API Key");
        }

        var apiKey = await _db.ApiKeys
            .Where(x => x.Key == key)
            .Select(x => new { x.CustomerId, x.Label })
            .FirstOrDefaultAsync();

        if (apiKey == null)
        {
            return AuthenticateResult.Fail("Invalid API Key");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiKey.CustomerId),
            new Claim("Label", apiKey.Label)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
