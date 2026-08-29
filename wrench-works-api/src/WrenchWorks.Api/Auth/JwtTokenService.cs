using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace WrenchWorks.Api.Auth;

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string email, Guid businessId, Guid businessUserId, IEnumerable<string> permissions, IEnumerable<string> features);
}

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public string GenerateToken(Guid userId, string email, Guid businessId, Guid businessUserId, IEnumerable<string> permissions, IEnumerable<string> features)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("email", email),
            new("business_id", businessId.ToString()),
            new("business_user_id", businessUserId.ToString()),
        };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        claims.AddRange(features.Select(f => new Claim("feature", f)));

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
