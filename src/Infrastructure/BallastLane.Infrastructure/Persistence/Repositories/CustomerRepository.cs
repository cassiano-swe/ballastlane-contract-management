using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using Npgsql;
using NpgsqlTypes;
namespace BallastLane.Infrastructure.Persistence.Repositories;
public sealed class CustomerRepository(NpgsqlDataSource dataSource) : ICustomerRepository
{
    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, name, trade_name, customer_type, tax_id, email, version, active
            FROM customers
            WHERE id = @id;
            """);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? MapCustomer(reader) : null;
    }

    public async Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, name, trade_name, customer_type, tax_id, email, version, active
            FROM customers
            ORDER BY name ASC, id ASC;
            """);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var customers = new List<Customer>();

        while (await reader.ReadAsync(cancellationToken))
        {
            customers.Add(MapCustomer(reader));
        }

        return customers;
    }

    private static Customer MapCustomer(NpgsqlDataReader reader) =>
        Customer.Reconstitute(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            (CustomerType)reader.GetInt16(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetGuid(6),
            reader.GetBoolean(7));

    public async Task<bool> UpdateAsync(Customer customer, Guid expectedVersion, Guid authorId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE customers
            SET name = @name, trade_name = @trade_name, customer_type = @customer_type,
                tax_id = @tax_id, email = @email, version = @new_version, updated_by_user_id = @author_id
            WHERE id = @id AND version = @expected_version;
            """);
        BindCustomer(command, customer, "new_version");
        command.Parameters.AddWithValue("author_id", NpgsqlDbType.Uuid, authorId);
        command.Parameters.AddWithValue("expected_version", NpgsqlDbType.Uuid, expectedVersion);
        return await ExecuteWriteAsync(command, cancellationToken) switch
        {
            1 => true,
            0 => false,
            _ => throw new InvalidOperationException("Customer update returned an unexpected affected-row count.")
        };
    }

    public async Task<bool> DeactivateAsync(Guid id, Guid expectedVersion, Guid newVersion, Guid authorId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE customers
            SET active = FALSE, version = @new_version, updated_by_user_id = @author_id
            WHERE id = @id AND version = @expected_version AND active = TRUE;
            """);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, id);
        command.Parameters.AddWithValue("expected_version", NpgsqlDbType.Uuid, expectedVersion);
        command.Parameters.AddWithValue("new_version", NpgsqlDbType.Uuid, newVersion);
        command.Parameters.AddWithValue("author_id", NpgsqlDbType.Uuid, authorId);
        return await command.ExecuteNonQueryAsync(cancellationToken) switch
        {
            1 => true,
            0 => false,
            _ => throw new InvalidOperationException("Customer deactivation returned an unexpected affected-row count.")
        };
    }

    public async Task CreateAsync(Customer customer, Guid authorId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO customers (id, name, trade_name, customer_type, tax_id, email, version, active, created_by_user_id)
            VALUES (@id, @name, @trade_name, @customer_type, @tax_id, @email, @version, @active, @author_id)
            """);
        BindCustomer(command, customer, "version");
        command.Parameters.AddWithValue("active", NpgsqlDbType.Boolean, customer.Active);
        command.Parameters.AddWithValue("author_id", NpgsqlDbType.Uuid, authorId);
        if (await ExecuteWriteAsync(command, cancellationToken) != 1)
            throw new InvalidOperationException("Customer insertion did not affect one row.");
    }

    private static void BindCustomer(NpgsqlCommand command, Customer customer, string versionParameter)
    {
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, customer.Id);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, customer.Name);
        command.Parameters.AddWithValue("trade_name", NpgsqlDbType.Varchar, (object?)customer.TradeName ?? DBNull.Value);
        command.Parameters.AddWithValue("customer_type", NpgsqlDbType.Smallint, (short)customer.CustomerType);
        command.Parameters.AddWithValue("tax_id", NpgsqlDbType.Varchar, customer.TaxId);
        command.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, customer.Email);
        command.Parameters.AddWithValue(versionParameter, NpgsqlDbType.Uuid, customer.Version);
    }

    private static async Task<int> ExecuteWriteAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        try
        {
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "uq_customers_tax_id")
        {
            throw new CustomerAlreadyExistsException();
        }
    }
}
