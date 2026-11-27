extern alias business;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;
namespace BallastLane.IntegrationTests;
public class CustomerTests
{
    [Fact] public async Task ApiPersistsAndEnforcesConcurrencyAndContracts()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var ds = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(ds);
        await TestJwt.SeedAuthorAsync(ds);
        await using var app = new WebApplicationFactory<business::Program>().WithWebHostBuilder(builder => {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = database.GetConnectionString(), ["Jwt:Issuer"] = TestJwt.Issuer, ["Jwt:Audience"] = TestJwt.Audience, ["Jwt:SigningKey"] = TestJwt.SigningKey }));
        });
        var client = app.CreateAuthenticatedClient();
        var body = new { name = " Acme ", tradeName = " Shop ", customerType = "Company", taxId = "001'23", email = "contact@acme.example" };
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/customers", body), client.PostAsJsonAsync("/api/customers", body));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var success = responses.Single(r => r.StatusCode == HttpStatusCode.Created);
        var json = JsonDocument.Parse(await success.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("001'23", json.GetProperty("taxId").GetString());
        Assert.Equal("Company", json.GetProperty("customerType").GetString());
        Assert.NotEqual(Guid.Empty, json.GetProperty("version").GetGuid());
        Assert.Equal("/api/customers/" + json.GetProperty("id").GetString(), success.Headers.Location?.ToString());
        var conflict = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType?.MediaType);
        Assert.True(JsonDocument.Parse(await conflict.Content.ReadAsStringAsync()).RootElement.TryGetProperty("traceId", out _));
        await using (var count = ds.CreateCommand("SELECT count(*) FROM customers WHERE tax_id = '001''23'")) Assert.Equal  (1L, await count.ExecuteScalarAsync());
        foreach (var invalidType in new object[] { 2, "2", "Invalid" })
        {
            var invalid = await client.PostAsJsonAsync("/api/customers", new { name = "Name", customerType = invalidType, taxId = "2", email = "a@example.com" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.True(JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()).RootElement.TryGetProperty("traceId", out _));
        }
        var swagger = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json")).RootElement;
        var operation = swagger.GetProperty("paths").GetProperty("/api/customers").GetProperty("post");
        Assert.True(Assert.Single(operation.GetProperty("security").EnumerateArray()).TryGetProperty("Bearer", out _));
        var requestSchema = swagger.GetProperty("components").GetProperty("schemas").GetProperty("CreateCustomerRequest");
        Assert.False(requestSchema.GetProperty("properties").TryGetProperty("version", out _));
        var versionSchema = swagger.GetProperty("components").GetProperty("schemas").GetProperty("CreateCustomerResponse")
            .GetProperty("properties").GetProperty("version");
        Assert.Equal("string", versionSchema.GetProperty("type").GetString());
        Assert.Equal("uuid", versionSchema.GetProperty("format").GetString());
        Assert.NotEqual(Guid.Empty, versionSchema.GetProperty("example").GetGuid());
        var schema = swagger.GetProperty("components").GetProperty("schemas").GetProperty("CustomerType");
        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(new[] { "Individual", "Company" }, schema.GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray());
    }
}
