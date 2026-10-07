namespace Jusoor.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
}
