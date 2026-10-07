using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Jusoor.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Jusoor.Infrastructure.Identity;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SigningKey { get; set; }
    public int AccessTokenMinutes { get; set; } = 30;
}

/// <summary>
/// Issues access + refresh tokens. The signing key is read from
/// configuration — in local dev that's `.env`/user-secrets, in
/// staging/production it MUST be Azure Key Vault per spec §22 ("no secrets
/// in source code"). This class never has a hardcoded fallback key: if
/// configuration is missing, startup fails loudly instead of running with a
/// guessable key.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IConfiguration configuration)
    {
        _options = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException(
                "Jwt configuration section is missing. Refusing to start with no signing key rather than falling back to a default.");
    }

    public AuthTokenResult GenerateTokens(string userId, string email, IReadOnlyList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateSecureRandomToken();

        return new AuthTokenResult(accessToken, expires, refreshToken);
    }

    private static string GenerateSecureRandomToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
