using FluentValidation;
using Jusoor.Application.Common.Interfaces;
using MediatR;

namespace Jusoor.Application.Auth;

public record LoginCommand(string Email, string Password) : IRequest<LoginResult>;

public record LoginResult(bool Succeeded, AuthTokenResult? Tokens);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenService _jwtTokenService;

    public LoginCommandHandler(IIdentityService identityService, IJwtTokenService jwtTokenService)
    {
        _identityService = identityService;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var check = await _identityService.CheckPasswordAsync(request.Email, request.Password);

        if (!check.Succeeded || check.UserId is null || check.Email is null)
        {
            // Deliberately generic failure — never distinguish "no such user"
            // from "wrong password" in the response. This is the
            // anti-enumeration principle the spec applies to Atmaen (§13)
            // applied here too, since login is the other obvious
            // enumeration surface.
            return new LoginResult(false, null);
        }

        var tokens = _jwtTokenService.GenerateTokens(check.UserId, check.Email, check.Roles);
        return new LoginResult(true, tokens);
    }
}
