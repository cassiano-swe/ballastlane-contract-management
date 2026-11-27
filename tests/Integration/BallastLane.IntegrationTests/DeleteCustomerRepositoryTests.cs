using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.Infrastructure.Persistence.Repositories;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class DeleteCustomerRepositoryTests
{
    [Fact]
    public async Task CompetingConnectionsPersistOnlyTheWinningDeactivation()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await using var otherSource = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        var repository = new CustomerRepository(source);
        var otherRepository = new CustomerRepository(otherSource);
        var customer = new Customer("Acme", "Shop", CustomerType.Company, "001", "a@example.com");
        await repository.CreateAsync(customer, default);
        var before = Assert.Single(await repository.ListAsync(default));
        Assert.True(before.Active);
        Assert.Equal(customer.Version, before.Version);
        var firstVersion = Guid.NewGuid();
        var secondVersion = Guid.NewGuid();
        var staleVersion = Guid.NewGuid();

        Assert.False(await repository.DeactivateAsync(customer.Id, staleVersion, firstVersion, default));
        var stillActive = Assert.Single(await repository.ListAsync(default));
        Assert.Equal(State(before), State(stillActive));
        Assert.Equal((true, before.Version), (stillActive.Active, stillActive.Version));

        var results = await Task.WhenAll(
            repository.DeactivateAsync(customer.Id, customer.Version, firstVersion, default),
            otherRepository.DeactivateAsync(customer.Id, customer.Version, secondVersion, default));

        Assert.Single(results, persisted => persisted);
        var winner = results[0] ? firstVersion : secondVersion;
        Assert.False(await repository.DeactivateAsync(customer.Id, winner, Guid.NewGuid(), default));
        Assert.False(await repository.DeactivateAsync(Guid.NewGuid(), winner, Guid.NewGuid(), default));
        var after = Assert.Single(await repository.ListAsync(default));
        Assert.Equal((false, winner), (after.Active, after.Version));
        Assert.Equal(State(before), State(after));
    }

    private static (Guid, string, string?, CustomerType, string, string) State(Customer customer) =>
        (customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email);
}
