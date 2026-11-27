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
            SELECT id, name, trade_name, customer_type, tax_id, email
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
            SELECT id, name, trade_name, customer_type, tax_id, email
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
            reader.GetString(5));

    public async Task CreateAsync(Customer customer, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("INSERT INTO customers (id, name, trade_name, customer_type, tax_id, email) VALUES (@id, @name, @trade_name, @customer_type, @tax_id, @email)");
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, customer.Id);
        command.Parameters.AddWithValue("name", NpgsqlDbType.Varchar, customer.Name);
        command.Parameters.AddWithValue("trade_name", NpgsqlDbType.Varchar, (object?)customer.TradeName ?? DBNull.Value);
        command.Parameters.AddWithValue("customer_type", NpgsqlDbType.Smallint, (short)customer.CustomerType);
        command.Parameters.AddWithValue("tax_id", NpgsqlDbType.Varchar, customer.TaxId);
        command.Parameters.AddWithValue("email", NpgsqlDbType.Varchar, customer.Email);
        try { if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Customer insertion did not affect one row."); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && ex.ConstraintName == "uq_customers_tax_id") { throw new CustomerAlreadyExistsException(); }
    }
}
