namespace BallastLane.Application.Abstractions.Security;

public sealed class AccessToken(string value, int expiresIn)
{
    public string Value { get; } = value;
    public int ExpiresIn { get; } = expiresIn;
}
