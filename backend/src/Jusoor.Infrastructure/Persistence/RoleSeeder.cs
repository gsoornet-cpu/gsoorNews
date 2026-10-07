using Jusoor.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Jusoor.Infrastructure.Persistence;

/// <summary>
/// Seeds the newsroom roles as Identity roles so Phase 3's RBAC work has
/// them ready. Deliberately idempotent (checks RoleExistsAsync) so it's safe
/// to run on every startup, not just the first one.
/// </summary>
public static class RoleSeeder
{
    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in NewsroomRole.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }
}
