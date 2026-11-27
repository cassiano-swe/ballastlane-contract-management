namespace BallastLane.Application.UseCases.Users.LoginUser;

public sealed class LoginUserOutput(string accessToken, int expiresIn)
{
    public string AccessToken { get; } = accessToken;
    public string TokenType => "Bearer";
    public int ExpiresIn { get; } = expiresIn;
}
