using BallastLane.Application.Abstractions.Persistence;

namespace BallastLane.Application.UseCases.Customers.ListCustomers;

public sealed class ListCustomersHandler(ICustomerRepository repository)
{
    public async Task<IReadOnlyList<ListCustomersOutput>> HandleAsync(CancellationToken cancellationToken)
    {
        var customers = await repository.ListAsync(cancellationToken);
        return customers.Select(customer => new ListCustomersOutput(
            customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email)).ToArray();
    }
}
