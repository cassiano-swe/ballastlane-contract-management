using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public class UserMigrationTests
{
    [Fact]
    public async Task CreatesUsersWithExpectedConstraintsAndPreservesExistingCustomers()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAsync(source, "001_create_customers.sql");
        await CustomerMigrations.ApplyAsync(source, "002_add_customer_version.sql");
        await CustomerMigrations.ApplyAsync(source, "003_add_customer_active.sql");
        await using var seed = source.CreateCommand("""
            INSERT INTO customers (id, name, trade_name, customer_type, tax_id, email, version, active)
            VALUES (gen_random_uuid(), 'Alex', NULL, 1, '001', 'alex@example.com', gen_random_uuid(), TRUE),
                   (gen_random_uuid(), 'Acme', 'Shop', 2, '002', 'acme@example.com', gen_random_uuid(), FALSE);
            """);
        Assert.Equal(2, await seed.ExecuteNonQueryAsync());
        await using var snapshot = source.CreateCommand("SELECT json_agg(customers ORDER BY id)::text FROM customers");
        var before = Assert.IsType<string>(await snapshot.ExecuteScalarAsync());

        await CustomerMigrations.ApplyAsync(source, "004_create_users.sql");

        Assert.Equal(before, await snapshot.ExecuteScalarAsync());
        await using var count = source.CreateCommand("SELECT count(*) FROM users");
        Assert.Equal(0L, await count.ExecuteScalarAsync());
        await using var insert = source.CreateCommand("""
            INSERT INTO users (id, name, email, password_hash)
            VALUES (gen_random_uuid(), 'Example', 'user@example.com', 'synthetic-hash')
            RETURNING active;
            """);
        Assert.Equal(true, await insert.ExecuteScalarAsync());

        await AssertConstraintAsync(source, "name", null, PostgresErrorCodes.NotNullViolation);
        await AssertConstraintAsync(source, "email", null, PostgresErrorCodes.NotNullViolation);
        await AssertConstraintAsync(source, "password_hash", null, PostgresErrorCodes.NotNullViolation);
        await AssertConstraintAsync(source, "name", " ", PostgresErrorCodes.CheckViolation);
        await AssertConstraintAsync(source, "email", " ", PostgresErrorCodes.CheckViolation);
        await AssertConstraintAsync(source, "password_hash", " ", PostgresErrorCodes.CheckViolation);
        foreach (var (sql, column) in new[] { ("UPDATE users SET active = NULL", "active"), ("UPDATE users SET id = NULL", "id") })
        {
            await using var command = source.CreateCommand(sql);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.NotNullViolation, error.SqlState);
            Assert.Equal(column, error.ColumnName);
        }
    }

    private static async Task AssertConstraintAsync(NpgsqlDataSource source, string column, string? value, string sqlState)
    {
        await using var command = source.CreateCommand("""
            INSERT INTO users (id, name, email, password_hash) VALUES (gen_random_uuid(), @name, @email, @password_hash);
            """);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, column == "name" ? (object?)value ?? DBNull.Value : "Example");
        command.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, column == "email" ? (object?)value ?? DBNull.Value : "other@example.com");
        command.Parameters.AddWithValue("password_hash", NpgsqlDbType.Text, column == "password_hash" ? (object?)value ?? DBNull.Value : "synthetic-hash");
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(sqlState, error.SqlState);
        if (value is null)
            Assert.Equal(column, error.ColumnName);
        else
            Assert.Equal($"ck_users_{column}", error.ConstraintName);
    }
}
