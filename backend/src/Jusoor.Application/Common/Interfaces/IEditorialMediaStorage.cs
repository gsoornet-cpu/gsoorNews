namespace Jusoor.Application.Common.Interfaces;

public sealed record SignedMediaUpload(
    string Url, string Token, string Bucket, string ObjectPath, string ResumableEndpoint, DateTimeOffset ExpiresAtUtc);

public sealed record StoredMediaObject(long SizeBytes, string ContentType, byte[] PrefixBytes);

public sealed class EditorialMediaStorageException(string failureCode) : Exception(failureCode)
{
    public string FailureCode { get; } = failureCode;
}

/// <summary>Storage boundary for editorial media. Credentials and vendor HTTP details stay in Infrastructure.</summary>
public interface IEditorialMediaStorage
{
    Task<SignedMediaUpload> CreateSignedUploadAsync(string objectPath, CancellationToken cancellationToken);
    Task<StoredMediaObject?> InspectPublicObjectAsync(string objectPath, CancellationToken cancellationToken);
    Task DeleteObjectAsync(string objectPath, CancellationToken cancellationToken);
    string GetPublicUrl(string objectPath);
}
