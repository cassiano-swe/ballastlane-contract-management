using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class CustomerActiveMigrationTests
{
    [Fact]
    public async Task BackfillsActivePreservesExistingStateAndEnforcesDefaultAndNotNull()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAsync(source, "001_create_customers.sql");
        await CustomerMigrations.ApplyAsync(source, "002_add_customer_version.sql");
        await using var seed = source.CreateCommand("""
            INSERT INTO customers (id, name, trade_name, customer_type, tax_id, email, version)
            VALUES (gen_random_uuid(), 'Alex', NULL, 1, '001', 'alex@example.com', gen_random_uuid()),
                   (gen_random_uuid(), 'Acme', 'Shop', 2, '002', 'acme@example.com', gen_random_uuid());
            """);
        Assert.Equal(2, await seed.ExecuteNonQueryAsync());
        await using var snapshot = source.CreateCommand("""
            SELECT json_agg(row_to_json(existing) ORDER BY existing.id)::text
            FROM (SELECT id, name, trade_name, customer_type, tax_id, email, version FROM customers) AS existing;
            """);
        var before = Assert.IsType<string>(await snapshot.ExecuteScalarAsync());

        await CustomerMigrations.ApplyAsync(source, "003_add_customer_active.sql");

        Assert.Equal(before, await snapshot.ExecuteScalarAsync());
        await using var activeCount = source.CreateCommand("SELECT count(*) FROM customers WHERE active IS TRUE");
        Assert.Equal(2L, await activeCount.ExecuteScalarAsync());
        await using var insert = source.CreateCommand("""
            INSERT INTO customers (id, name, customer_type, tax_id, email, version)
            VALUES (gen_random_uuid(), 'New', 1, '003', 'new@example.com', gen_random_uuid())
            RETURNING active;
            """);
        Assert.Equal(true, await insert.ExecuteScalarAsync());
        await using var invalid = source.CreateCommand("UPDATE customers SET active = NULL WHERE tax_id = '003'");
        var failure = await Assert.ThrowsAsync<PostgresException>(() => invalid.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.NotNullViolation, failure.SqlState);
    }
}
