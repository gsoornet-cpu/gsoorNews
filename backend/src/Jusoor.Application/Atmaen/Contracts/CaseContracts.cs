using Jusoor.Domain.Enums;

namespace Jusoor.Application.Atmaen.Contracts;

/// <summary>
/// One row in a CrisisEditor's own assigned-case queue. Deliberately omits
/// ApplicantPhoneE164 and SubjectContactPhone — a CrisisEditor triaging their
/// queue doesn't need contact numbers until they open a specific case, same
/// "don't expose more than the list view needs" reasoning ReviewQueueItemDto
/// already applies (excerpt only, not full article bodies).
/// </summary>
public sealed record CaseSummaryDto(
    Guid CaseId,
    CaseStatus Status,
    string SubjectName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AssignedAtUtc,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn);

/// <summary>
/// Full detail for the single-case screen — includes contact numbers, unlike
/// CaseSummaryDto, because a CrisisEditor actually working a case needs them
/// to make contact per spec §10 step 7.
/// </summary>
public sealed record CaseDetailDto(
    Guid CaseId,
    CaseStatus Status,
    string SubjectName,
    string? SubjectContactPhone,
    string ConcernDescription,
    string ApplicantPhoneE164,
    DateTimeOffset OtpVerifiedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? AssignedAtUtc,
    string? CountryNameAr,
    string? CountryNameEn,
    string? CityNameAr,
    string? CityNameEn);
