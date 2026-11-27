extern alias business;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BallastLane.IntegrationTests;
public class CustomerValidationContractTests
{
    [Fact]
    public async Task IndividualWithTradeNameExplainsRuleInDetail()
    {
        await using var app = new WebApplicationFactory<business::Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development"); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = TestJwt.Issuer, ["Jwt:Audience"] = TestJwt.Audience, ["Jwt:SigningKey"] = TestJwt.SigningKey, ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test" })); });
        using var client = app.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Acme Corporation", tradeName = "Acme", customerType = "Individual", taxId = "00123456789", email = "contact@acme.example" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Contains("Trade name is only allowed for companies.", json.GetProperty("detail").GetString());
        Assert.True(json.GetProperty("errors").TryGetProperty("TradeName", out _));
        Assert.True(json.TryGetProperty("traceId", out _));
    }
    [Fact]
    public async Task InvalidEnumExplainsAcceptedValuesWithoutEchoingInput()
    {
        await using var app = new WebApplicationFactory<business::Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development"); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = TestJwt.Issuer, ["Jwt:Audience"] = TestJwt.Audience, ["Jwt:SigningKey"] = TestJwt.SigningKey, ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test" })); });
        using var client = app.CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/customers", new { name = "Name", customerType = "sensitive-invalid-value", taxId = "001", email = "a@example.com" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var detail = json.GetProperty("detail").GetString();
        Assert.Contains("Individual or Company", detail);
        Assert.DoesNotContain("sensitive-invalid-value", detail);
    }
    [Fact]
    public async Task SwaggerExplainsPermittedCustomerTypes()
    {
        await using var app = new WebApplicationFactory<business::Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development"); builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Issuer"] = TestJwt.Issuer, ["Jwt:Audience"] = TestJwt.Audience, ["Jwt:SigningKey"] = TestJwt.SigningKey, ["ConnectionStrings:Postgres"] = "Host=localhost;Database=test;Username=test;Password=test" })); });
        using var client = app.CreateAuthenticatedClient();
        var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json")).RootElement;
        var description = swagger.GetProperty("paths").GetProperty("/api/customers").GetProperty("post").GetProperty("description").GetString();
        Assert.Contains("Individual", description);
        Assert.Contains("Company", description);
        var customerType = swagger.GetProperty("components").GetProperty("schemas").GetProperty("CustomerType");
        Assert.Equal(new[] { "Individual", "Company" }, customerType.GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray());
    }
}
