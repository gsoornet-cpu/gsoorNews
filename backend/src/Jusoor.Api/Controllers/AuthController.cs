using Jusoor.Application.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Jusoor.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _mediator;

    public AuthController(ISender mediator)
    {
        _mediator = mediator;
    }

    public record RegisterRequest(string Email, string Password, string DisplayName);
    public record LoginRequest(string Email, string Password);

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RegisterCommand(request.Email, request.Password, request.DisplayName),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return Ok(result.Tokens);
    }

    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);

        if (!result.Succeeded)
        {
            // Generic message on purpose — see LoginCommandHandler's comment
            // on anti-enumeration.
            return Unauthorized(new { message = "Invalid credentials." });
        }

        return Ok(result.Tokens);
    }
}
