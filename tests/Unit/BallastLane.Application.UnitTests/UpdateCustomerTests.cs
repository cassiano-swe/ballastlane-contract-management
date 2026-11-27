using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Application.UseCases.Customers.UpdateCustomer;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace BallastLane.Application.UnitTests;

public class UpdateCustomerTests
{
    private static readonly Guid AuthorId = Guid.NewGuid();
    private readonly ICustomerRepository repository = Substitute.For<ICustomerRepository>();
    private readonly Customer customer = new("Old", null, CustomerType.Individual, "001", "old@example.com");

    private UpdateCustomerInput Input => new(customer.Id, " New ", " Shop ", CustomerType.Company, " 001'23 ", " new@example.com ", customer.Version);
    private UpdateCustomerHandler Handler => new(new(), repository);

    [Fact]
    public async Task WritesNormalizedStateWithOriginalVersionAndReturnsPersistedVersion()
    {
        using var source = new CancellationTokenSource();
        var input = Input;
        repository.GetByIdAsync(customer.Id, source.Token).Returns(customer);
        repository.UpdateAsync(customer, input.Version, AuthorId, source.Token).Returns(true);

        var result = await Handler.HandleAsync(input, AuthorId, source.Token);

        Assert.Equal(new UpdateCustomerOutput(customer.Id, "New", "Shop", CustomerType.Company, "001'23", "new@example.com", customer.Version), result);
        Assert.NotEqual(input.Version, result.Version);
        await repository.Received(1).GetByIdAsync(customer.Id, source.Token);
        await repository.Received(1).UpdateAsync(customer, input.Version, AuthorId, source.Token);
        Assert.Equal(2, repository.ReceivedCalls().Count());
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Version")]
    [InlineData("Name")]
    [InlineData("TradeName")]
    [InlineData("CustomerType")]
    [InlineData("TaxId")]
    [InlineData("Email")]
    public async Task InvalidFieldsAreRejectedBeforeAccessingRepository(string property)
    {
        UpdateCustomerInput[] invalid = property switch
        {
            "Id" => [Input with { Id = Guid.Empty }],
            "Version" => [Input with { Version = Guid.Empty }],
            "Name" => [Input with { Name = null }, Input with { Name = " " }, Input with { Name = new string('n', 201) }],
            "TradeName" => [Input with { TradeName = new string('t', 201) }, Input with { CustomerType = CustomerType.Individual }],
            "CustomerType" => [Input with { CustomerType = (CustomerType)99 }],
            "TaxId" => [Input with { TaxId = null }, Input with { TaxId = " " }, Input with { TaxId = new string('1', 51) }],
            _ => [Input with { Email = null }, Input with { Email = " " }, Input with { Email = "invalid" }, Input with { Email = new string('a', 245) + "@example.com" }]
        };

        foreach (var input in invalid)
        {
            var failure = await Assert.ThrowsAsync<ValidationException>(() => Handler.HandleAsync(input, AuthorId, default));
            Assert.Contains(failure.Errors, error => error.PropertyName == property);
        }
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task MissingCustomerDoesNotWrite()
    {
        repository.GetByIdAsync(customer.Id, default).Returns((Customer?)null);
        await Assert.ThrowsAsync<CustomerNotFoundException>(() => Handler.HandleAsync(Input, AuthorId, default));
        Assert.Single(repository.ReceivedCalls());
    }

    [Fact]
    public async Task StaleVersionDoesNotMutateOrWrite()
    {
        var version = customer.Version;
        repository.GetByIdAsync(customer.Id, default).Returns(customer);
        await Assert.ThrowsAsync<CustomerConcurrencyException>(() => Handler.HandleAsync(Input with { Version = Guid.NewGuid() }, AuthorId, default));
        Assert.Equal(("Old", version), (customer.Name, customer.Version));
        Assert.Single(repository.ReceivedCalls());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroRowsRechecksExistenceAndNeverRetries(bool stillExists)
    {
        using var source = new CancellationTokenSource();
        var input = Input;
        repository.GetByIdAsync(customer.Id, source.Token).Returns(customer, stillExists ? customer : null);
        repository.UpdateAsync(customer, input.Version, AuthorId, source.Token).Returns(false);

        if (stillExists)
            await Assert.ThrowsAsync<CustomerConcurrencyException>(() => Handler.HandleAsync(input, AuthorId, source.Token));
        else
            await Assert.ThrowsAsync<CustomerNotFoundException>(() => Handler.HandleAsync(input, AuthorId, source.Token));

        await repository.Received(2).GetByIdAsync(customer.Id, source.Token);
        await repository.Received(1).UpdateAsync(customer, input.Version, AuthorId, source.Token);
        Assert.Equal(3, repository.ReceivedCalls().Count());
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unexpected")]
    [InlineData("cancelled")]
    public async Task WriteFailurePropagatesWithoutRetry(string scenario)
    {
        Exception failure = scenario switch
        {
            "duplicate" => new CustomerAlreadyExistsException(),
            "cancelled" => new OperationCanceledException(),
            _ => new InvalidOperationException()
        };
        var input = Input;
        repository.GetByIdAsync(customer.Id, default).Returns(customer);
        repository.UpdateAsync(customer, input.Version, AuthorId, default).Returns(Task.FromException<bool>(failure));

        Assert.Same(failure, await Record.ExceptionAsync(() => Handler.HandleAsync(input, AuthorId, default)));
        Assert.Equal(2, repository.ReceivedCalls().Count());
    }
}
