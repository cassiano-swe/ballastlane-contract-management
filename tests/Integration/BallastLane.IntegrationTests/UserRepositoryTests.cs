using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Domain.Entities;
using BallastLane.Infrastructure;
using BallastLane.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class UserRepositoryTests
{
    private const string SyntheticHash = "  synthetic-test-hash\t";

    [Fact]
    public async Task PersistsAllFieldsAndReadsActiveAndInactiveUsersByNormalizedEmail()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = database.GetConnectionString()
        }).Build();
        await using var services = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
        var source = services.GetRequiredService<NpgsqlDataSource>();
        await CustomerMigrations.ApplyAllAsync(source);
        await using var scope = services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var token = timeout.Token;
        var user = new User(" Example Person ", " Example+Alias@Example.COM ", SyntheticHash);
        await repository.CreateAsync(user, token);

        await using var query = source.CreateCommand("SELECT id, name, email, password_hash, active FROM users");
        await using (var reader = await query.ExecuteReaderAsync(token))
        {
            Assert.True(await reader.ReadAsync(token));
            Assert.Equal((user.Id, "Example Person", "example+alias@example.com", SyntheticHash, true),
                (reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4)));
            Assert.False(await reader.ReadAsync(token));
        }

        foreach (var email in new[] { user.Email, "EXAMPLE+ALIAS@EXAMPLE.COM", "  example+alias@example.com  " })
            Assert.Equal(State(user), State(Assert.IsType<User>(await repository.GetByEmailAsync(email, token))));
        Assert.Null(await repository.GetByEmailAsync("missing@example.com", token));

        var inactive = User.Reconstitute(Guid.NewGuid(), "Inactive person", "inactive@example.com", SyntheticHash, false);
        await repository.CreateAsync(inactive, token);
        Assert.Equal(State(inactive), State(Assert.IsType<User>(await repository.GetByEmailAsync(inactive.Email, token))));
    }

    [Fact]
    public async Task ConcurrentCreatesWithEquivalentEmailsPersistExactlyOneUser()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        var connection = new NpgsqlConnectionStringBuilder(database.GetConnectionString()) { IncludeErrorDetail = true };
        await using var firstSource = NpgsqlDataSource.Create(connection.ConnectionString);
        await using var secondSource = NpgsqlDataSource.Create(connection.ConnectionString);
        await CustomerMigrations.ApplyAllAsync(firstSource);
        var firstRepository = new UserRepository(firstSource);
        var secondRepository = new UserRepository(secondSource);
        var first = new User("First", " race@EXAMPLE.com ", SyntheticHash);
        var second = new User("Second", "RACE@example.com", "other-synthetic-hash");
        Assert.NotEqual(first.Id, second.Id);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var results = await Task.WhenAll(
            Record.ExceptionAsync(() => firstRepository.CreateAsync(first, timeout.Token)),
            Record.ExceptionAsync(() => secondRepository.CreateAsync(second, timeout.Token)));

        Assert.Single(results, result => result is null);
        var duplicate = Assert.IsType<UserAlreadyExistsException>(Assert.Single(results.OfType<Exception>()));
        Assert.DoesNotContain(first.Email, duplicate.Message);
        Assert.DoesNotContain(first.PasswordHash, duplicate.Message);
        Assert.DoesNotContain(second.PasswordHash, duplicate.Message);
        Assert.Null(duplicate.InnerException);
        var winner = results[0] is null ? first : second;
        Assert.Equal(State(winner), State(Assert.IsType<User>(await firstRepository.GetByEmailAsync(winner.Email, default))));
        await using var count = firstSource.CreateCommand("SELECT count(*) FROM users");
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task PrimaryKeyConflictIsNotTranslatedAsDuplicateEmail()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        var connection = new NpgsqlConnectionStringBuilder(database.GetConnectionString()) { IncludeErrorDetail = true };
        await using var source = NpgsqlDataSource.Create(connection.ConnectionString);
        await CustomerMigrations.ApplyAllAsync(source);
        var repository = new UserRepository(source);
        var original = new User("Original", "original@example.com", SyntheticHash);
        var duplicateId = User.Reconstitute(original.Id, "Different", "different@example.com", SyntheticHash, true);
        await repository.CreateAsync(original, default);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(duplicateId, default));

        Assert.DoesNotContain(original.Id.ToString(), error.Message);
        Assert.DoesNotContain(SyntheticHash, error.Message);
        Assert.Null(error.InnerException);
        Assert.Null(await repository.GetByEmailAsync(duplicateId.Email, default));
    }

    private static (Guid, string, string, string, bool) State(User user) =>
        (user.Id, user.Name, user.Email, user.PasswordHash, user.Active);
}
