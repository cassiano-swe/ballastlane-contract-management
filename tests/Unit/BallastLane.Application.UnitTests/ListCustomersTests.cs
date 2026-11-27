using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.UseCases.Customers.ListCustomers;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using NSubstitute;
using Xunit;

namespace BallastLane.Application.UnitTests;

public class ListCustomersTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task MapsCustomersInRepositoryOrderAndPropagatesToken(int count)
    {
        var repository = Substitute.For<ICustomerRepository>();
        using var source = new CancellationTokenSource();
        Customer[] customers =
        [
            new("Zeta", null, CustomerType.Individual, "001", "a@example.com"),
            new("Acme", "Shop", CustomerType.Company, "002", "b@example.com")
        ];
        var stored = customers[..count];
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Customer>>(stored));

        var result = await new ListCustomersHandler(repository).HandleAsync(source.Token);

        Assert.Equal(stored.Select(customer => new ListCustomersOutput(
            customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email)), result);
        await repository.Received(1).ListAsync(Arg.Any<CancellationToken>());
        await repository.Received().ListAsync(source.Token);
    }

    [Fact]
    public async Task PropagatesUnexpectedRepositoryFailure()
    {
        var repository = Substitute.For<ICustomerRepository>();
        var failure = new InvalidOperationException("Repository failure.");
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Customer>>(failure));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ListCustomersHandler(repository).HandleAsync(default));

        Assert.Same(failure, thrown);
    }
}
