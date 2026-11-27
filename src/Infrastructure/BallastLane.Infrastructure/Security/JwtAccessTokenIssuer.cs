using BallastLane.Application.Abstractions.Security;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BallastLane.Infrastructure.Security;

public sealed class JwtAccessTokenIssuer(JwtSettings settings, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new() { SetDefaultTimesOnTokenCreation = false };

    public AccessToken Issue(Guid userId)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            Expires = now.AddSeconds(settings.LifetimeSeconds),
            SigningCredentials = settings.SigningCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
            }
        });
        return new AccessToken(token, settings.LifetimeSeconds);
    }
}
