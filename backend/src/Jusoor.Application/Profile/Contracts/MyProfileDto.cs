namespace Jusoor.Application.Profile.Contracts;

public sealed record MyPlaceDto(
    Guid CityId,
    string CityNameAr,
    string CityNameEn,
    string? CountryNameAr,
    string? CountryNameEn,
    DateTimeOffset FollowedAtUtc);

/// <summary>
/// PrivacyLevel is exposed as its enum name (string), not an int — the
/// same "don't make the API contract depend on remembering what 2 means"
/// reasoning that keeps every other DTO in this codebase using resolved
/// strings/names rather than raw codes wherever a human reads the response.
/// </summary>
public sealed record MyProfileDto(
    string PrivacyLevel,
    string? CountryNameAr,
    string? CountryNameEn,
    string? RegionNameAr,
    string? RegionNameEn,
    string? CityNameAr,
    string? CityNameEn,
    IReadOnlyList<MyPlaceDto> Places);
