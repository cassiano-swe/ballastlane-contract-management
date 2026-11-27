using Npgsql;

namespace BallastLane.IntegrationTests;

internal static class CustomerMigrations
{
    private static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "migrations");

    public static async Task ApplyAllAsync(NpgsqlDataSource dataSource)
    {
        foreach (var path in Directory.GetFiles(DirectoryPath, "*.sql").OrderBy(path => path, StringComparer.Ordinal))
            await ExecuteAsync(dataSource, path);
    }

    public static Task ApplyAsync(NpgsqlDataSource dataSource, string script) =>
        ExecuteAsync(dataSource, Path.Combine(DirectoryPath, script));

    private static async Task ExecuteAsync(NpgsqlDataSource dataSource, string path)
    {
        await using var command = dataSource.CreateCommand(await File.ReadAllTextAsync(path));
        await command.ExecuteNonQueryAsync();
    }
}
