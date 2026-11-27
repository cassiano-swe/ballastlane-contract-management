namespace BallastLane.Application.Abstractions.Security;

public interface IAccessTokenIssuer
{
    AccessToken Issue(Guid userId);
}
