using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CoreBPM.Server.Application.Auth.Interfaces;
using CoreBPM.Server.Domain.Auth;

namespace CoreBPM.Server.Application.Auth.Services;

/// <summary>Сервис генерации и валидации JWT и refresh-токенов.</summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public string GenerateAccessToken(AuthAccount account, IEnumerable<string> roles)
    {
        var secretKey = _configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey не настроен");

        var issuer = _configuration["Jwt:Issuer"] ?? "CoreBPM";
        var audience = _configuration["Jwt:Audience"] ?? "CoreBPM";
        var lifetimeMinutes = _configuration.GetValue<int>("Jwt:AccessTokenLifetimeMinutes", 30);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, account.User.DisplayName),
            new(JwtRegisteredClaimNames.Email, account.User.WorkEmail),
        };

        // Добавляем роли как отдельные claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncoder.Encode(bytes);
    }

    /// <inheritdoc />
    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var secretKey = _configuration["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
            return null;

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        try
        {
            // Валидируем без проверки времени жизни (для refresh-сценария)
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"] ?? "CoreBPM",
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"] ?? "CoreBPM",
                ValidateLifetime = false
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
