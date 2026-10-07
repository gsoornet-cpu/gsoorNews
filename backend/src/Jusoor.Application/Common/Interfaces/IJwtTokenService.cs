namespace Jusoor.Application.Common.Interfaces;

public record AuthTokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc, string RefreshToken);

public interface IJwtTokenService
{
    AuthTokenResult GenerateTokens(string userId, string email, IReadOnlyList<string> roles);
}
