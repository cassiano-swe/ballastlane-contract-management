namespace BallastLane.Application.UseCases.Users.LoginUser;

public sealed class LoginUserInput(string? email, string? password)
{
    public string? Email { get; } = email;
    public string? Password { get; } = password;
}
