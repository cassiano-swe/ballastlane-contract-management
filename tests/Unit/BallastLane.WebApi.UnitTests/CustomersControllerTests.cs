using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.UseCases.Customers.CreateCustomer;
using BallastLane.Application.UseCases.Customers.ListCustomers;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.WebApi.Controllers;
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
        var controller = new CustomersController(new CreateCustomerHandler(new(), repository), new ListCustomersHandler(repository));
        using var source = new CancellationTokenSource();

        var result = await controller.List(source.Token);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsAssignableFrom<IReadOnlyList<ListCustomersResponse>>(ok.Value);
        if (populated)
            Assert.Equal(new ListCustomersResponse(customer.Id, "Acme", "Shop", CustomerType.Company, "001", "a@example.com"),
                Assert.Single(response));
        else
            Assert.Empty(response);
        await repository.Received(1).ListAsync(source.Token);
    }
}
