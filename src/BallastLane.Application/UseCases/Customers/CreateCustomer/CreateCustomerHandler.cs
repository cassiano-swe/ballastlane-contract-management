using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Domain.Entities;
using FluentValidation;
namespace BallastLane.Application.UseCases.Customers.CreateCustomer;
public sealed class CreateCustomerHandler(CreateCustomerValidator validator, ICustomerRepository repository)
{
    public async Task<CreateCustomerOutput> HandleAsync(CreateCustomerInput input, Guid authorId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(authorId, Guid.Empty);
        var normalized = input with { Name = input.Name?.Trim(), TradeName = string.IsNullOrWhiteSpace(input.TradeName) ? null : input.TradeName.Trim(), TaxId = input.TaxId?.Trim(), Email = input.Email?.Trim() };
        await validator.ValidateAndThrowAsync(normalized, cancellationToken);
        var customer = new Customer(normalized.Name, normalized.TradeName, normalized.CustomerType, normalized.TaxId, normalized.Email);
        await repository.CreateAsync(customer, authorId, cancellationToken);
        return new(customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version);
    }
}
