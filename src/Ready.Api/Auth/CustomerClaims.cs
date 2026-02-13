using System.Security.Claims;

namespace Ready.Api.Auth;

public static class CustomerClaims
{
    public static string? GetCustomerId(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
