using Jusoor.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Jusoor.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Application.Common.Interfaces.IdentityResult> CreateUserAsync(
        string email, string password, string displayName)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName
        };

        var result = await _userManager.CreateAsync(user, password);

        return new Application.Common.Interfaces.IdentityResult(
            result.Succeeded,
            result.Succeeded ? user.Id : null,
            result.Errors.Select(e => e.Description).ToList());
    }

    public async Task<UserCredentialsCheckResult> CheckPasswordAsync(string email, string password)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return new UserCredentialsCheckResult(false, null, null, Array.Empty<string>());
        }

        // Uses CheckPasswordAsync (not SignInManager) deliberately: Phase 0
        // is stateless-JWT only. Lockout-on-failure semantics come with
        // SignInManager in Phase 2 alongside Atmaen's anti-enumeration/rate
        // limiting hardening, not bolted on ad hoc here.
        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            return new UserCredentialsCheckResult(false, null, null, Array.Empty<string>());
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new UserCredentialsCheckResult(true, user.Id, user.Email, roles.ToList());
    }

    public async Task<NewsroomUserPage> ListUsersAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(term)) || u.DisplayName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<NewsroomUserInfo>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new NewsroomUserInfo(user.Id, user.Email, user.DisplayName, roles.ToList()));
        }

        return new NewsroomUserPage(items, total);
    }

    public async Task<NewsroomUserInfo?> FindUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        return new NewsroomUserInfo(user.Id, user.Email, user.DisplayName, roles.ToList());
    }

    public async Task<bool> AddToRoleAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is not null && (await _userManager.AddToRoleAsync(user, role)).Succeeded;
    }

    public async Task<bool> RemoveFromRoleAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is not null && (await _userManager.RemoveFromRoleAsync(user, role)).Succeeded;
    }

    public async Task<IReadOnlyList<string>> GetUserIdsInRoleAsync(string role)
    {
        var users = await _userManager.GetUsersInRoleAsync(role);
        return users.Select(u => u.Id).ToList();
    }
}
