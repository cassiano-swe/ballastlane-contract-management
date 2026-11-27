using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Application.UseCases.Customers.GetCustomerById;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace BallastLane.Application.UnitTests;

public class GetCustomerByIdTests
{
    [Fact]
    public void ValidatorAcceptsNonEmptyId()
    {
        Assert.True(new GetCustomerByIdValidator().Validate(new GetCustomerByIdInput(Guid.NewGuid())).IsValid);
    }

    [Fact]
    public async Task EmptyIdFailsValidationBeforeCallingRepository()
    {
        var repository = Substitute.For<ICustomerRepository>();
        var input = new GetCustomerByIdInput(Guid.Empty);
        var validator = new GetCustomerByIdValidator();

        Assert.Equal(nameof(input.Id), Assert.Single(validator.Validate(input).Errors).PropertyName);
        await Assert.ThrowsAsync<ValidationException>(() => new GetCustomerByIdHandler(validator, repository)
            .HandleAsync(input, default));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Theory]
    [InlineData(null, CustomerType.Individual)]
    [InlineData("Shop", CustomerType.Company)]
    public async Task MapsAllFieldsAndPassesIdAndTokenOnce(string? tradeName, CustomerType type)
    {
        var id = Guid.NewGuid();
        var version = Guid.NewGuid();
        var customer = Customer.Reconstitute(id, "Alex Taylor", tradeName, type, "001'23", "alex@example.com", version, tradeName is null);
        using var source = new CancellationTokenSource();
        var repository = Substitute.For<ICustomerRepository>();
        repository.GetByIdAsync(id, source.Token).Returns(Task.FromResult<Customer?>(customer));

        var result = await new GetCustomerByIdHandler(new(), repository).HandleAsync(new(id), source.Token);

        Assert.Equal(new GetCustomerByIdOutput(id, "Alex Taylor", tradeName, type, "001'23", "alex@example.com", version, tradeName is null), result);
        await repository.Received(1).GetByIdAsync(id, source.Token);
        Assert.Single(repository.ReceivedCalls());
    }

    [Fact]
    public async Task MissingCustomerThrowsAfterOneLookup()
    {
        var repository = Substitute.For<ICustomerRepository>();
        var id = Guid.NewGuid();
        repository.GetByIdAsync(id, default).Returns(Task.FromResult<Customer?>(null));

        await Assert.ThrowsAsync<CustomerNotFoundException>(() =>
            new GetCustomerByIdHandler(new(), repository).HandleAsync(new(id), default));
        await repository.Received(1).GetByIdAsync(id, default);
        Assert.Single(repository.ReceivedCalls());
    }

    [Fact]
    public async Task UnexpectedRepositoryFailurePropagates()
    {
        var repository = Substitute.For<ICustomerRepository>();
        var id = Guid.NewGuid();
        var failure = new InvalidOperationException("Repository failure.");
        repository.GetByIdAsync(id, default).Returns(Task.FromException<Customer?>(failure));

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetCustomerByIdHandler(new(), repository).HandleAsync(new(id), default));
        Assert.Same(failure, actual);
    }
}
