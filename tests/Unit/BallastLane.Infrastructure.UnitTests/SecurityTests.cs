using System.Security.Cryptography;
using BallastLane.Domain.Entities;
using BallastLane.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace BallastLane.Infrastructure.UnitTests;

public sealed class SecurityTests
{
    [Fact]
    public void HashesWithUniqueSaltsAndVerifiesExactPassword()
    {
        var hasher = new UserPasswordHasher();
        const string password = " password with spaces ";
        var hash = hasher.Hash(password);
        Assert.NotEqual(hash, hasher.Hash(password));

        var user = new User("User", "example@example.com", hash);
        Assert.True(hasher.Verify(user, password));
        Assert.False(hasher.Verify(user, password.Trim()));
        Assert.False(hasher.Verify(new User("User", user.Email, "invalid!"), password));
        Assert.False(hasher.Verify(new User("User", user.Email, Convert.ToBase64String([0])), password));
        hasher.VerifyDummy(password);

        var oldHasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
        {
            CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV2
        }));
        var oldHash = oldHasher.HashPassword(user, password);
        Assert.True(hasher.Verify(new User("User", user.Email, oldHash), password));
    }

    [Fact]
    public async Task IssuesSignedTokensWithOnlyExpectedClaimsAndConfiguredLifetime()
    {
        var settings = JwtSettings.Load(Configuration());
        var now = DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds());
        var issuer = new JwtAccessTokenIssuer(settings, new FixedTimeProvider(now));
        var userId = Guid.NewGuid();
        var token = issuer.Issue(userId);
        var handler = new JsonWebTokenHandler();
        var parsed = handler.ReadJsonWebToken(token.Value);
        Assert.Equal(1800, token.ExpiresIn);
        Assert.Equal("HS256", parsed.Alg);
        Assert.Equal(userId.ToString(), parsed.Subject);
        Assert.Equal(settings.Issuer, parsed.Issuer);
        Assert.Equal(settings.Audience, Assert.Single(parsed.Audiences));
        Assert.Equal(now.UtcDateTime, parsed.IssuedAt);
        Assert.Equal(now.AddSeconds(token.ExpiresIn).UtcDateTime, parsed.ValidTo);
        Assert.NotEqual(parsed.Id, handler.ReadJsonWebToken(issuer.Issue(userId).Value).Id);
        Assert.Equal(new[] { "aud", "exp", "iat", "iss", "jti", "sub" }, parsed.Claims.Select(claim => claim.Type).Order());
        Assert.True((await handler.ValidateTokenAsync(token.Value, settings.CreateValidationParameters())).IsValid);
    }

    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:SigningKey", "invalid")]
    [InlineData("Jwt:SigningKey", "c2hvcnQ=")]
    [InlineData("Jwt:AccessTokenLifetimeSeconds", "0")]
    [InlineData("Jwt:AccessTokenLifetimeSeconds", "bad")]
    public void RejectsInvalidSettings(string setting, string value)
    {
        var configuration = Configuration();
        configuration[setting] = value;
        var error = Assert.Throws<InvalidOperationException>(() => JwtSettings.Load(configuration));
        Assert.Contains(setting, error.Message);
        if (setting == "Jwt:SigningKey")
            Assert.DoesNotContain(value, error.Message);
    }

    private static IConfigurationRoot Configuration() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "issuer", ["Jwt:Audience"] = "audience",
            ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
