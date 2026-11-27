using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class CustomerVersionMigrationTests
{
    [Fact]
    public async Task BackfillsVersionsAndPreservesLegacyCustomers()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var dataSource = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAsync(dataSource, "001_create_customers.sql");
        await using (var seed = dataSource.CreateCommand("""
            INSERT INTO customers (id, name, trade_name, customer_type, tax_id, email)
            VALUES ('10000000-0000-0000-0000-000000000001', 'Alex Taylor', NULL, 1, '001', 'alex@example.com'),
                   ('10000000-0000-0000-0000-000000000002', 'Acme', 'Shop', 2, '002', 'acme@example.com')
            """))
        {
            Assert.Equal(2, await seed.ExecuteNonQueryAsync());
        }

        var before = await ReadBusinessDataAsync(dataSource);
        await CustomerMigrations.ApplyAsync(dataSource, "002_add_customer_version.sql");
        var after = await ReadBusinessDataAsync(dataSource);
        Assert.Equal(before, after);

        await using (var query = dataSource.CreateCommand("SELECT version FROM customers ORDER BY id"))
        await using (var reader = await query.ExecuteReaderAsync())
        {
            var versions = new HashSet<Guid>();
            while (await reader.ReadAsync())
            {
                var version = reader.GetGuid(0);
                Assert.NotEqual(Guid.Empty, version);
                Assert.True(versions.Add(version));
            }
            Assert.Equal(2, versions.Count);
        }

        await AssertInvalidVersionAsync(dataSource, null, PostgresErrorCodes.NotNullViolation);
        await AssertInvalidVersionAsync(dataSource, Guid.Empty, PostgresErrorCodes.CheckViolation);
    }

    private static async Task<string> ReadBusinessDataAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT json_agg(row_to_json(legacy) ORDER BY legacy.id)::text
            FROM (SELECT id, name, trade_name, customer_type, tax_id, email FROM customers) AS legacy
            """);
        return Assert.IsType<string>(await command.ExecuteScalarAsync());
    }

    private static async Task AssertInvalidVersionAsync(NpgsqlDataSource dataSource, Guid? version, string expectedState)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO customers (id, name, customer_type, tax_id, email, version)
            VALUES ('10000000-0000-0000-0000-000000000003', 'Sample', 1, '003', 'sample@example.com', @version)
            """);
        command.Parameters.AddWithValue("version", NpgsqlDbType.Uuid, (object?)version ?? DBNull.Value);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(expectedState, failure.SqlState);
    }
}
