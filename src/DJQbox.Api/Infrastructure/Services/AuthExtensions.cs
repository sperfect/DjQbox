using System.Security.Claims;

namespace DJQbox.Api.Infrastructure.Services;

public static class AuthExtensions
{
    public static Guid? GetUserId(this HttpContext context)
    {
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
               ?? context.User.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    public static string? GetRole(this HttpContext context)
    {
        return context.User.FindFirst(ClaimTypes.Role)?.Value
            ?? context.User.FindFirst("role")?.Value;
    }

    public static HashSet<string> GetScopes(this HttpContext context)
    {
        return context.User.FindAll("scope")
            .Concat(context.User.FindAll("scp"))
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasScope(this HttpContext context, string requiredScope)
    {
        var role = context.GetRole();
        if (string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return context.GetScopes().Contains(requiredScope);
    }
}