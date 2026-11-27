using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using FluentValidation;

namespace BallastLane.Application.UseCases.Customers.DeleteCustomer;

public sealed class DeleteCustomerHandler(DeleteCustomerValidator validator, ICustomerRepository repository)
{
    public async Task HandleAsync(DeleteCustomerInput input, Guid authorId, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(authorId, Guid.Empty);
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var customer = await repository.GetByIdAsync(input.Id, cancellationToken)
            ?? throw new CustomerNotFoundException();
        var expectedVersion = input.Version;
        if (customer.Version != expectedVersion)
            throw new CustomerConcurrencyException();
        if (!customer.Active)
            return;

        customer.Deactivate();
        if (!await repository.DeactivateAsync(customer.Id, expectedVersion, customer.Version, authorId, cancellationToken))
        {
            if (await repository.GetByIdAsync(input.Id, cancellationToken) is null)
                throw new CustomerNotFoundException();
            throw new CustomerConcurrencyException();
        }
    }
}
