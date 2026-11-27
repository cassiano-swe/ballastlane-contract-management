using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.UseCases.Customers.CreateCustomer;
using BallastLane.Application.UseCases.Customers.GetCustomerById;
using BallastLane.Application.UseCases.Customers.ListCustomers;
using BallastLane.Application.UseCases.Customers.UpdateCustomer;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.WebApi.Controllers;
using BallastLane.WebApi.Models.Requests.Customers;
using BallastLane.WebApi.Models.Responses.Customers;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace BallastLane.WebApi.UnitTests;

public class CustomersControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ListReturnsOkWithMappedCustomersAndPropagatesToken(bool populated)
    {
        var repository = Substitute.For<ICustomerRepository>();
        var customer = new Customer("Acme", "Shop", CustomerType.Company, "001", "a@example.com");
        IReadOnlyList<Customer> customers = populated ? [customer] : [];
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(customers));
        var controller = CreateController(repository);
        using var source = new CancellationTokenSource();

        var result = await controller.List(source.Token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<ListCustomersResponse>>(ok.Value);
        if (populated)
            Assert.Equal(new ListCustomersResponse(customer.Id, "Acme", "Shop", CustomerType.Company, "001", "a@example.com", customer.Version),
                Assert.Single(response));
        else
            Assert.Empty(response);
        await repository.Received(1).ListAsync(source.Token);
    }

    [Fact]
    public async Task GetByIdReturnsOkWithMappedCustomerAndPropagatesIdAndToken()
    {
        var repository = Substitute.For<ICustomerRepository>();
        var id = Guid.NewGuid();
        var version = Guid.NewGuid();
        var customer = Customer.Reconstitute(id, "Acme", "Shop", CustomerType.Company, "001", "a@example.com", version);
        using var source = new CancellationTokenSource();
        repository.GetByIdAsync(id, source.Token).Returns(Task.FromResult<Customer?>(customer));

        var result = await CreateController(repository).GetById(id, source.Token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(new GetCustomerByIdResponse(id, "Acme", "Shop", CustomerType.Company, "001", "a@example.com", version),
            Assert.IsType<GetCustomerByIdResponse>(ok.Value));
        await repository.Received(1).GetByIdAsync(id, source.Token);
    }

    [Fact]
    public async Task UpdateMapsRouteAndBodyAndReturnsNewVersion()
    {
        var repository = Substitute.For<ICustomerRepository>();
        var customer = new Customer("Old", null, CustomerType.Individual, "001", "old@example.com");
        var expectedVersion = customer.Version;
        using var source = new CancellationTokenSource();
        repository.GetByIdAsync(customer.Id, source.Token).Returns(customer);
        repository.UpdateAsync(customer, expectedVersion, source.Token).Returns(true);

        var result = await CreateController(repository).Update(customer.Id, new UpdateCustomerRequest
        {
            Name = "New", TradeName = "Shop", CustomerType = CustomerType.Company,
            TaxId = "002", Email = "new@example.com", Version = expectedVersion
        }, source.Token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(new UpdateCustomerResponse(customer.Id, "New", "Shop", CustomerType.Company, "002",
            "new@example.com", customer.Version), Assert.IsType<UpdateCustomerResponse>(ok.Value));
        Assert.NotEqual(expectedVersion, customer.Version);
        await repository.Received(1).GetByIdAsync(customer.Id, source.Token);
        await repository.Received(1).UpdateAsync(customer, expectedVersion, source.Token);
    }

    private static CustomersController CreateController(ICustomerRepository repository) =>
        new(new CreateCustomerHandler(new(), repository), new ListCustomersHandler(repository),
            new GetCustomerByIdHandler(new(), repository), new UpdateCustomerHandler(new(), repository));
}
