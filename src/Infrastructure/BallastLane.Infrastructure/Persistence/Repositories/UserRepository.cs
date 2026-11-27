using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Domain.Entities;
using Npgsql;
using NpgsqlTypes;

namespace BallastLane.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(NpgsqlDataSource dataSource) : IUserRepository
{
    public async Task CreateAsync(User user, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO users (id, name, email, password_hash, active)
            VALUES (@id, @name, @email, @password_hash, @active);
            """);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, user.Id);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, user.Name);
        command.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, user.Email);
        command.Parameters.AddWithValue("password_hash", NpgsqlDbType.Text, user.PasswordHash);
        command.Parameters.AddWithValue("active", NpgsqlDbType.Boolean, user.Active);

        try
        {
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("User insertion did not affect one row.");
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation && exception.ConstraintName == "uq_users_email")
        {
            throw new UserAlreadyExistsException();
        }
        catch (NpgsqlException)
        {
            // PostgreSQL diagnostics can include row values when error detail is enabled.
            throw new InvalidOperationException("Could not persist the user.");
        }
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, name, email, password_hash, active
            FROM users
            WHERE email = @email;
            """);
        command.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, User.NormalizeEmail(email));

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return User.Reconstitute(reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetBoolean(4));
        }
        catch (NpgsqlException)
        {
            throw new InvalidOperationException("Could not read the user.");
        }
    }
}
