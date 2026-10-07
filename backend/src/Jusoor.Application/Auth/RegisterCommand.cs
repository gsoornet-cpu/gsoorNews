using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using MediatR;

namespace Jusoor.Application.Auth;

public record RegisterCommand(string Email, string Password, string DisplayName) : IRequest<RegisterResult>;

public record RegisterResult(bool Succeeded, IReadOnlyList<string> Errors, AuthTokenResult? Tokens);

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);

        // Baseline complexity rule; production tuning (breach-list checks,
        // etc.) belongs in Phase 2 alongside Atmaen hardening, not invented
        // here without a source in the spec.
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);

        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;

    public RegisterCommandHandler(IIdentityService identityService, IJwtTokenService jwtTokenService)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<RegisterResult> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var result = await _identityService.CreateUserAsync(request.Email, request.Password, request.DisplayName);

        if (!result.Succeeded || result.UserId is null)
        {
            return new RegisterResult(false, result.Errors, null);
        }

        // New self-registrations have no newsroom role by default — roles are
        // granted explicitly (Phase 3 CMS scope), never assumed at signup.
        var tokens = _jwtTokenService.GenerateTokens(result.UserId, request.Email, Array.Empty<string>());

        return new RegisterResult(true, Array.Empty<string>(), tokens);
    }
}
