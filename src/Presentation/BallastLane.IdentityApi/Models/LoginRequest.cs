namespace BallastLane.IdentityApi.Models;

public sealed class LoginRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
}
