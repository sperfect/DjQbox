using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DJQbox.Api.Infrastructure.Services;

public class TokenService(IConfiguration config)
{
    private readonly string _signingKey = config["Jwt:SigningKey"] ?? "poc-signing-key-change-in-production-min-32-chars!";

    public string GenerateToken(Guid userId, string role, Guid venueId, IEnumerable<string> scopes, TimeSpan? expiry = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.Role, role),
            new("role", role),
            new("venue_id", venueId.ToString())
        };

        foreach (var scope in scopes)
        {
            claims.Add(new Claim("scope", scope));
            claims.Add(new Claim("scp", scope));
        }

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry ?? TimeSpan.FromDays(30)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }
}