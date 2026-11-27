using BallastLane.Application.UseCases.Users.LoginUser;
using BallastLane.IdentityApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BallastLane.IdentityApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(LoginUserHandler handler) : ControllerBase
{
    /// <summary>Authenticates an active user and issues an access token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, "application/problem+json")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LoginUserInput(request.Email, request.Password), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(new LoginResponse(result.AccessToken, result.TokenType, result.ExpiresIn));
    }
}
