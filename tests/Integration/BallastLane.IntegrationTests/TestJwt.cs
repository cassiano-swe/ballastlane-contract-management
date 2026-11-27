using System.Net.Http.Headers;
using System.Security.Cryptography;
using BallastLane.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace BallastLane.IntegrationTests;

internal static class TestJwt
{
    public const string Issuer = "ballastlane-tests";
    public const string Audience = "ballastlane-tests-api";
    public static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public static readonly Guid UserId = Guid.NewGuid();

    public static HttpClient CreateAuthenticatedClient<T>(this WebApplicationFactory<T> factory) where T : class
    {
        var settings = JwtSettings.Load(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience, ["Jwt:SigningKey"] = SigningKey
            }).Build());
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", new JwtAccessTokenIssuer(settings, TimeProvider.System).Issue(UserId).Value);
        return client;
    }
}
