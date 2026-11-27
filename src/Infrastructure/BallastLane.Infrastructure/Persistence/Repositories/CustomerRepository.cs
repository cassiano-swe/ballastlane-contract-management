using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Domain.Entities;
using Npgsql;
using NpgsqlTypes;
namespace BallastLane.Infrastructure.Persistence.Repositories;
public sealed class CustomerRepository(NpgsqlDataSource dataSource) : ICustomerRepository
{
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
