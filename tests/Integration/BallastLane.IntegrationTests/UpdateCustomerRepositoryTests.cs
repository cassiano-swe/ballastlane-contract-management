using BallastLane.Application.Exceptions;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.Infrastructure.Persistence.Repositories;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class UpdateCustomerRepositoryTests
{
    [Fact]
    public async Task CompetingWritesPersistExactlyOneCompleteState()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var firstSource = NpgsqlDataSource.Create(database.GetConnectionString());
        await using var secondSource = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(firstSource);
        var firstRepository = new CustomerRepository(firstSource);
        var secondRepository = new CustomerRepository(secondSource);
        var original = new Customer("Old", "Shop", CustomerType.Company, "001", "old@example.com");
        await firstRepository.CreateAsync(original, default);
        var first = Assert.IsType<Customer>(await firstRepository.GetByIdAsync(original.Id, default));
        var second = Assert.IsType<Customer>(await secondRepository.GetByIdAsync(original.Id, default));
        Assert.Equal(original.Version, first.Version);
        Assert.Equal(original.Version, second.Version);
        first.Update("First", null, CustomerType.Individual, "001'23", "first@example.com");
        second.Update("Second", "Store", CustomerType.Company, "002'34", "second@example.com");

        var results = await Task.WhenAll(firstRepository.UpdateAsync(first, original.Version, default),
            secondRepository.UpdateAsync(second, original.Version, default));

        Assert.Single(results, success => success);
        Assert.Single(results, success => !success);
        var winner = results[0] ? first : second;
        var loser = results[0] ? second : first;
        Assert.Equal(State(winner), State(Assert.IsType<Customer>(await firstRepository.GetByIdAsync(original.Id, default))));

        Assert.False(await firstRepository.UpdateAsync(loser, original.Version, default));
        Assert.Equal(State(winner), State(Assert.IsType<Customer>(await firstRepository.GetByIdAsync(original.Id, default))));

        var missing = new Customer("Missing", null, CustomerType.Individual, "003", "test@example.com");
        var missingVersion = missing.Version;
        missing.Update("Still missing", null, CustomerType.Individual, "003", "test@example.com");
        Assert.False(await firstRepository.UpdateAsync(missing, missingVersion, default));
    }

    [Fact]
    public async Task RetainingTaxIdSucceedsAndDuplicatePreservesPersistedState()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        var repository = new CustomerRepository(source);
        var customer = new Customer("Old", null, CustomerType.Individual, "001", "old@example.com");
        var other = new Customer("Other", null, CustomerType.Individual, "002", "other@example.com");
        await repository.CreateAsync(customer, default);
        await repository.CreateAsync(other, default);
        var originalVersion = customer.Version;
        customer.Update("New", "Shop", CustomerType.Company, "001", "new@example.com");
        Assert.True(await repository.UpdateAsync(customer, originalVersion, default));
        var persisted = State(customer);
        Assert.Equal(persisted, State(Assert.IsType<Customer>(await repository.GetByIdAsync(customer.Id, default))));

        customer.Update("Duplicate", null, CustomerType.Individual, other.TaxId, "duplicate@example.com");

        await Assert.ThrowsAsync<CustomerAlreadyExistsException>(() => repository.UpdateAsync(customer, persisted.Version, default));
        Assert.Equal(persisted, State(Assert.IsType<Customer>(await repository.GetByIdAsync(customer.Id, default))));
    }

    private static (Guid Id, string Name, string? TradeName, CustomerType Type, string TaxId, string Email, Guid Version) State(Customer customer) =>
        (customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version);
}
