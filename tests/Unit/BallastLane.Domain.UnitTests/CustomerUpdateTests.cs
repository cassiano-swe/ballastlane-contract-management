using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using Xunit;

namespace BallastLane.Domain.UnitTests;

public class CustomerUpdateTests
{
    [Fact]
    public void ReplacesEditableStateAndRotatesVersionEvenForIdenticalValues()
    {
        var customer = new Customer("Acme", "Shop", CustomerType.Company, "001", "old@example.com");
        var id = customer.Id;
        var original = customer.Version;

        customer.Update(" Alex ", " ", CustomerType.Individual, " 002 ", " new@example.com ");

        Assert.Equal((id, "Alex", null, CustomerType.Individual, "002", "new@example.com"),
            (customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email));
        Assert.NotEqual(Guid.Empty, customer.Version);
        Assert.NotEqual(original, customer.Version);
        var current = customer.Version;
        customer.Update("Alex", null, CustomerType.Individual, "002", "new@example.com");
        Assert.NotEqual(current, customer.Version);
        customer.Update("Alex Co", "Store", CustomerType.Company, "002", "new@example.com");
        Assert.Equal(CustomerType.Company, customer.CustomerType);
        Assert.Equal("Store", customer.TradeName);
        customer.Update("Alex Co", null, CustomerType.Company, "002", "new@example.com");
        Assert.Null(customer.TradeName);
    }

    [Theory]
    [InlineData(" ", null, CustomerType.Individual, "002", "new@example.com")]
    [InlineData("New", "Shop", CustomerType.Individual, "002", "new@example.com")]
    [InlineData("New", null, (CustomerType)99, "002", "new@example.com")]
    [InlineData("New", null, CustomerType.Individual, " ", "new@example.com")]
    [InlineData("New", null, CustomerType.Individual, "002", "invalid")]
    public void InvalidUpdatePreservesAllState(string name, string? tradeName, CustomerType type, string taxId, string email)
    {
        var customer = new Customer("Acme", "Shop", CustomerType.Company, "001", "old@example.com");
        var original = (customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version);

        Assert.Throws<ArgumentException>(() => customer.Update(name, tradeName, type, taxId, email));

        Assert.Equal(original, (customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email, customer.Version));
    }
}
