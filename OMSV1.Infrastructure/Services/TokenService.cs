using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OMSV1.Infrastructure.Identity;
using OMSV1.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using OMSV1.Infrastructure.Persistence;

namespace OMSV1.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;
    private readonly SymmetricSecurityKey _key;

    public TokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager, AppDbContext context)
    {
        _configuration = configuration;
        _userManager = userManager;
        _context = context;

        var tokenKey = configuration["TokenKey"] ?? throw new Exception("Cannot Access Token Key from app settings");
        if (tokenKey.Length < 64) throw new Exception("Your tokenkey needs to be longer");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenKey));
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime AccessTokenExpires, DateTime RefreshTokenExpires)> CreateToken(ApplicationUser user)
    {
        if (user.UserName == null) throw new Exception("No Username For User");

        // Add basic claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName)
        };

        // Add roles as claims
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        // Fetch role-based permissions
        var rolePermissions = await _context.RolePermissions
            .Where(rp => roles.Contains(rp.Role.Name))
            .Select(rp => rp.Permission)
            .Distinct()
            .ToListAsync();

        // Fetch user-specific permissions
        var userPermissions = await _context.UserPermissions
            .Where(up => up.UserId == user.Id)
            .Select(up => up.Permission)
            .ToListAsync();

        // Combine role-based and user-specific permissions
        var allPermissions = rolePermissions.Union(userPermissions).Distinct();

        // Add all permissions as claims
        claims.AddRange(allPermissions.Select(permission => new Claim("Permission", permission)));

        // Create signing credentials
        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);

        // Token expiration
        var accessTokenExpiry = DateTime.UtcNow.AddMinutes(2);
        var refreshTokenExpiry = DateTime.UtcNow.AddDays(7);

        // Create access token descriptor
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = accessTokenExpiry,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var accessToken = tokenHandler.CreateToken(tokenDescriptor);

        // Generate refresh token
        var refreshToken = GenerateRefreshToken();

        // Save refresh token to user
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = refreshTokenExpiry;
        await _userManager.UpdateAsync(user);

        return (tokenHandler.WriteToken(accessToken), refreshToken, accessTokenExpiry, refreshTokenExpiry);
    }

    public async Task<(string AccessToken, string RefreshToken, DateTime AccessTokenExpires, DateTime RefreshTokenExpires)?> RefreshToken(string accessToken, string refreshToken)
    {
        var principal = GetPrincipalFromExpiredToken(accessToken);
        if (principal == null) return null;

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return null;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || 
            user.RefreshToken != refreshToken || 
            user.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            return null;
        }

        return await CreateToken(user);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha512,
                    StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
