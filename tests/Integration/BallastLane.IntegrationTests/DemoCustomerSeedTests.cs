using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace BallastLane.IntegrationTests;

public sealed class DemoCustomerSeedTests
{
    [Fact]
    public async Task DemoCustomerSeedIsIdempotentAndMatchesTheDocumentedComposition()
    {
        await using var database = new PostgreSqlBuilder().WithImage("postgres:17").Build();
        await database.StartAsync();
        await using var source = NpgsqlDataSource.Create(database.GetConnectionString());
        await CustomerMigrations.ApplyAllAsync(source);
        await ExecuteSeedAsync(source, "001_create_demo_user.sql");
        await ExecuteSeedAsync(source, "002_create_demo_customers.sql");

        var first = await SnapshotAsync(source);
        await ExecuteSeedAsync(source, "002_create_demo_customers.sql");
        var second = await SnapshotAsync(source);

        Assert.Equal(first, second);
        Assert.Equal(15, first.Count);
        Assert.Equal(6, first.Count(row => row.Type == 1 && row.Active));
        Assert.Equal(2, first.Count(row => row.Type == 1 && !row.Active));
        Assert.Equal(5, first.Count(row => row.Type == 2 && row.Active));
        Assert.Equal(2, first.Count(row => row.Type == 2 && !row.Active));
        Assert.Equal(15, first.Select(row => row.Id).Distinct().Count());
        Assert.Equal(15, first.Select(row => row.TaxId).Distinct().Count());
        Assert.All(first, row =>
        {
            Assert.NotEqual(Guid.Empty, row.Version);
            Assert.Equal(Guid.Parse("4a23620c-6994-4d67-80cc-9b347568f585"), row.CreatedBy);
            Assert.Null(row.UpdatedBy);
        });
    }

    private static async Task ExecuteSeedAsync(NpgsqlDataSource source, string filename)
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "seeds", filename));
        await using var command = source.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<List<Row>> SnapshotAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("""
            SELECT id, customer_type, tax_id, version, active, created_by_user_id, updated_by_user_id
            FROM customers
            ORDER BY id;
            """);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Row>();
        while (await reader.ReadAsync())
        {
            rows.Add(new Row(reader.GetGuid(0), reader.GetInt16(1), reader.GetString(2), reader.GetGuid(3),
                reader.GetBoolean(4), reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6)));
        }
        return rows;
    }

    private sealed record Row(Guid Id, short Type, string TaxId, Guid Version, bool Active, Guid CreatedBy, Guid? UpdatedBy);
}
