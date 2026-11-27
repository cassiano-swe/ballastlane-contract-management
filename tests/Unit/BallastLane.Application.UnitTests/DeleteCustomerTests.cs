using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Application.UseCases.Customers.DeleteCustomer;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace BallastLane.Application.UnitTests;

public class DeleteCustomerTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private readonly ICustomerRepository repository = Substitute.For<ICustomerRepository>();

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InvalidInputDoesNotReachRepository(bool emptyId, bool emptyVersion)
    {
        var input = new DeleteCustomerInput(emptyId ? Guid.Empty : Guid.NewGuid(),
            emptyVersion ? Guid.Empty : Guid.NewGuid());

        await Assert.ThrowsAsync<ValidationException>(() => new DeleteCustomerHandler(new(), repository).HandleAsync(input, AuthorId, default));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ChecksVersionBeforeDecidingWhetherToWrite(bool active, bool stale)
    {
        var id = Guid.NewGuid();
        var version = Guid.NewGuid();
        var customer = Customer.Reconstitute(id, "Acme", null, CustomerType.Company, "001", "a@example.com", version, active);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        repository.GetByIdAsync(id, token).Returns(customer);
        repository.DeactivateAsync(id, version, Arg.Any<Guid>(), AuthorId, token).Returns(true);
        var handler = new DeleteCustomerHandler(new(), repository);
        var input = new DeleteCustomerInput(id, stale ? Guid.NewGuid() : version);

        if (stale)
            await Assert.ThrowsAsync<CustomerConcurrencyException>(() => handler.HandleAsync(input, AuthorId, token));
        else
            await handler.HandleAsync(input, AuthorId, token);

        await repository.Received(1).GetByIdAsync(id, token);
        if (active && !stale)
        {
            Assert.False(customer.Active);
            Assert.NotEqual(version, customer.Version);
            await repository.Received(1).DeactivateAsync(id, version, customer.Version, AuthorId, token);
            Assert.Equal(2, repository.ReceivedCalls().Count());
        }
        else
        {
            Assert.Equal((active, version), (customer.Active, customer.Version));
            Assert.Single(repository.ReceivedCalls());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ClassifiesFailedConditionalWriteWithoutRetry(bool disappeared)
    {
        var customer = new Customer("Acme", null, CustomerType.Company, "001", "a@example.com");
        var input = new DeleteCustomerInput(customer.Id, customer.Version);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        repository.GetByIdAsync(input.Id, token).Returns(customer, disappeared ? null : customer);
        repository.DeactivateAsync(input.Id, input.Version, Arg.Any<Guid>(), AuthorId, token).Returns(false);
        var handler = new DeleteCustomerHandler(new(), repository);

        if (disappeared)
            await Assert.ThrowsAsync<CustomerNotFoundException>(() => handler.HandleAsync(input, AuthorId, token));
        else
            await Assert.ThrowsAsync<CustomerConcurrencyException>(() => handler.HandleAsync(input, AuthorId, token));

        await repository.Received(2).GetByIdAsync(input.Id, token);
        await repository.Received(1).DeactivateAsync(input.Id, input.Version, customer.Version, AuthorId, token);
        Assert.Equal(3, repository.ReceivedCalls().Count());
    }

    [Fact]
    public async Task MissingCustomerDoesNotReachWrite()
    {
        var input = new DeleteCustomerInput(Guid.NewGuid(), Guid.NewGuid());
        repository.GetByIdAsync(input.Id, default).Returns((Customer?)null);

        await Assert.ThrowsAsync<CustomerNotFoundException>(() => new DeleteCustomerHandler(new(), repository).HandleAsync(input, AuthorId, default));
        Assert.Single(repository.ReceivedCalls());
    }

    [Fact]
    public async Task PropagatesUnexpectedWriteFailure()
    {
        var customer = new Customer("Acme", null, CustomerType.Company, "001", "a@example.com");
        var input = new DeleteCustomerInput(customer.Id, customer.Version);
        var failure = new InvalidOperationException("Repository failure.");
        repository.GetByIdAsync(input.Id, default).Returns(customer);
        repository.DeactivateAsync(input.Id, input.Version, Arg.Any<Guid>(), AuthorId, default).Returns(Task.FromException<bool>(failure));

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new DeleteCustomerHandler(new(), repository).HandleAsync(input, AuthorId, default)));
        Assert.Equal(2, repository.ReceivedCalls().Count());
    }
}
