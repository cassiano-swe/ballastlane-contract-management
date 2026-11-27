namespace BallastLane.IdentityApi.Models;

public sealed class LoginResponse(string accessToken, string tokenType, int expiresIn)
{
    public string AccessToken { get; } = accessToken;
    public string TokenType { get; } = tokenType;
    public int ExpiresIn { get; } = expiresIn;
}
