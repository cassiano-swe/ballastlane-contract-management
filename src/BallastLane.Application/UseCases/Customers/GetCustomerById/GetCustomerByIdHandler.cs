using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using FluentValidation;

namespace BallastLane.Application.UseCases.Customers.GetCustomerById;

public sealed class GetCustomerByIdHandler(GetCustomerByIdValidator validator, ICustomerRepository repository)
{
    public async Task<GetCustomerByIdOutput> HandleAsync(GetCustomerByIdInput input, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);
        var customer = await repository.GetByIdAsync(input.Id, cancellationToken)
            ?? throw new CustomerNotFoundException();

        return new(customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version);
    }
}
