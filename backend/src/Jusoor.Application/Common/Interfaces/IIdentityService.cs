namespace Jusoor.Application.Common.Interfaces;

public record IdentityResult(bool Succeeded, string? UserId, IReadOnlyList<string> Errors);

public record UserCredentialsCheckResult(bool Succeeded, string? UserId, string? Email, IReadOnlyList<string> Roles);

/// <summary>
/// Wraps ASP.NET Identity's UserManager/SignInManager so the Application
/// layer's Auth commands stay framework-agnostic and unit-testable. The
/// concrete implementation lives in Infrastructure.
/// </summary>
public record NewsroomUserInfo(string Id, string? Email, string DisplayName, IReadOnlyList<string> Roles);

public record NewsroomUserPage(IReadOnlyList<NewsroomUserInfo> Items, int TotalCount);

public interface IIdentityService
{
    /// <summary>Users (optionally filtered by email/display-name substring), newest first, with their roles.</summary>
    Task<NewsroomUserPage> ListUsersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<NewsroomUserInfo?> FindUserAsync(string userId);

    /// <summary>Returns false if the user does not exist or the role could not be added.</summary>
    Task<bool> AddToRoleAsync(string userId, string role);

    Task<bool> RemoveFromRoleAsync(string userId, string role);

    Task<IdentityResult> CreateUserAsync(string email, string password, string displayName);
    Task<UserCredentialsCheckResult> CheckPasswordAsync(string email, string password);

    /// <summary>User IDs of every account currently holding the given
    /// NewsroomRole. Added for Atmaen's temporary CrisisEditor assignment
    /// rotation (Phase 2 decision 3) — order is not guaranteed and callers
    /// must not rely on it.</summary>
    Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role);
}
