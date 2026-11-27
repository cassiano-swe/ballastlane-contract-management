using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using FluentValidation;

namespace BallastLane.Application.UseCases.Customers.UpdateCustomer;

public sealed class UpdateCustomerHandler(UpdateCustomerValidator validator, ICustomerRepository repository)
{
    public async Task<UpdateCustomerOutput> HandleAsync(UpdateCustomerInput input, CancellationToken cancellationToken)
    {
        var normalized = input with
        {
            Name = input.Name?.Trim(),
            TradeName = string.IsNullOrWhiteSpace(input.TradeName) ? null : input.TradeName.Trim(),
            TaxId = input.TaxId?.Trim(),
            Email = input.Email?.Trim()
        };
        await validator.ValidateAndThrowAsync(normalized, cancellationToken);

        var customer = await repository.GetByIdAsync(input.Id, cancellationToken)
            ?? throw new CustomerNotFoundException();
        var expectedVersion = input.Version;
        if (customer.Version != expectedVersion)
            throw new CustomerConcurrencyException();

        customer.Update(normalized.Name, normalized.TradeName, normalized.CustomerType, normalized.TaxId, normalized.Email);
        if (!await repository.UpdateAsync(customer, expectedVersion, cancellationToken))
        {
            if (await repository.GetByIdAsync(input.Id, cancellationToken) is null)
                throw new CustomerNotFoundException();
            throw new CustomerConcurrencyException();
        }

        return new(customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version);
    }
}
