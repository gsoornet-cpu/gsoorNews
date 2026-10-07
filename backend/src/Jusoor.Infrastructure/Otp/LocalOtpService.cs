using System.Collections.Concurrent;
using System.Security.Cryptography;
using Jusoor.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Jusoor.Infrastructure.Otp;

public sealed class LocalOtpService : IOtpService
{
    private const int CodeLength = 6;
    private static readonly TimeSpan Expiration = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Entry> _codes = new();

    public Task<OtpChallengeResult> SendVerificationAsync(
        string phoneNumberE164,
        CancellationToken cancellationToken)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        _codes[phoneNumberE164] = new Entry(code, DateTimeOffset.UtcNow.Add(Expiration));

        // Development only: print the OTP so local development can continue
        // without a real SMS provider. Never use this implementation in production.
        Console.WriteLine($"[LOCAL OTP] {phoneNumberE164}: {code}");

        return Task.FromResult(
            new OtpChallengeResult(
                $"local-{Guid.NewGuid():N}",
                OtpVerificationStatus.Pending));
    }

    public Task<OtpCheckResult> CheckVerificationAsync(
        string phoneNumberE164,
        string code,
        CancellationToken cancellationToken)
    {
        if (!_codes.TryGetValue(phoneNumberE164, out var entry))
        {
            return Task.FromResult(
                new OtpCheckResult(OtpVerificationStatus.Denied));
        }

        if (DateTimeOffset.UtcNow > entry.ExpiresAt)
        {
            _codes.TryRemove(phoneNumberE164, out _);

            return Task.FromResult(
                new OtpCheckResult(OtpVerificationStatus.Expired));
        }

        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(entry.Code),
                System.Text.Encoding.UTF8.GetBytes(code)))
        {
            return Task.FromResult(
                new OtpCheckResult(OtpVerificationStatus.Denied));
        }

        _codes.TryRemove(phoneNumberE164, out _);

        return Task.FromResult(
            new OtpCheckResult(OtpVerificationStatus.Approved));
    }

    private sealed record Entry(string Code, DateTimeOffset ExpiresAt);
}
