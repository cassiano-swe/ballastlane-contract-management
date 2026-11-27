using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Exceptions;
using BallastLane.Application.UseCases.Customers.CreateCustomer;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using FluentValidation;
using NSubstitute;
using Xunit;
namespace BallastLane.Application.UnitTests;
public class CreateCustomerTests
{
    private static CreateCustomerInput Valid() => new(" Acme ", " Shop ", CustomerType.Company, " 001 ", " a@example.com ");
    [Fact] public async Task NormalizesPersistsMapsAndPropagatesCancellation()
    {
        var repository = Substitute.For<ICustomerRepository>();
        using var source = new CancellationTokenSource();
        var result = await new CreateCustomerHandler(new(), repository).HandleAsync(Valid(), source.Token);
        Assert.NotEqual(Guid.Empty, result.Version);
        await repository.Received(1).CreateAsync(Arg.Is<Customer>(x => x.Id == result.Id && x.Name == "Acme"
            && x.TaxId == "001" && x.TradeName == "Shop" && x.Version == result.Version && x.Active), source.Token);
        Assert.Equal("a@example.com", result.Email);
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")]
    public async Task InvalidNamePreventsPersistence(string? name)
    {
        var repository = Substitute.For<ICustomerRepository>();
        await Assert.ThrowsAsync<ValidationException>(() => new CreateCustomerHandler(new(), repository).HandleAsync(Valid() with { Name = name }, default));
        await repository.DidNotReceiveWithAnyArgs().CreateAsync(default!, default);
    }
    [Fact] public async Task DuplicatePropagates()
    {
        var repository = Substitute.For<ICustomerRepository>();
        repository.CreateAsync(Arg.Any<Customer>(), Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new CustomerAlreadyExistsException());
        await Assert.ThrowsAsync<CustomerAlreadyExistsException>(() => new CreateCustomerHandler(new(), repository).HandleAsync(Valid(), default));
    }
    [Theory]
    [InlineData(0)] [InlineData(3)]
    public void UndefinedTypeIsInvalid(int type) => Assert.False(new CreateCustomerValidator().Validate(Valid() with { CustomerType = (CustomerType)type }).IsValid);
    [Fact] public void IndividualCannotHaveTradeName() => Assert.False(new CreateCustomerValidator().Validate(Valid() with { CustomerType = CustomerType.Individual }).IsValid);
}
