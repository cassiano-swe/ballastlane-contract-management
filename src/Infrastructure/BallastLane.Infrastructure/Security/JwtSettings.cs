using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace BallastLane.Infrastructure.Security;

public sealed class JwtSettings
{
    private readonly SymmetricSecurityKey signingKey;

    private JwtSettings(string issuer, string audience, byte[] key, int lifetimeSeconds)
    {
        Issuer = issuer;
        Audience = audience;
        signingKey = new SymmetricSecurityKey(key);
        LifetimeSeconds = lifetimeSeconds;
    }

    public string Issuer { get; }
    public string Audience { get; }
    public int LifetimeSeconds { get; }
    internal SigningCredentials SigningCredentials => new(signingKey, SecurityAlgorithms.HmacSha256);

    public static JwtSettings Load(IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            throw new InvalidOperationException("Configure Jwt:Issuer and Jwt:Audience.");

        var encoded = configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(encoded))
            throw InvalidKey();

        byte[] key;
        try
        {
            key = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw InvalidKey();
        }

        if (key.Length < 32)
            throw InvalidKey();

        var lifetime = 1800;
        var configuredLifetime = configuration["Jwt:AccessTokenLifetimeSeconds"];
        if (configuredLifetime is not null && (!int.TryParse(configuredLifetime, out lifetime) || lifetime <= 0))
            throw new InvalidOperationException("Jwt:AccessTokenLifetimeSeconds must be a positive integer.");

        return new JwtSettings(issuer, audience, key, lifetime);
    }

    public TokenValidationParameters CreateValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        IgnoreTrailingSlashWhenValidatingAudience = false,
        RequireSignedTokens = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "sub"
    };

    private static InvalidOperationException InvalidKey() =>
        new("Jwt:SigningKey must be a Base64-encoded key of at least 32 bytes.");
}
