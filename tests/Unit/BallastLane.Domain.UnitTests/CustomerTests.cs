using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using Xunit;

namespace BallastLane.Domain.UnitTests;

public class CustomerTests
{
    [Fact]
    public void ReconstitutesCustomerWithPersistedIdentityAndText()
    {
        var id = Guid.Parse("078c5ce5-120b-4afa-a6c3-770dfeec1f3c");
        var customer = Customer.Reconstitute(id, " Name ", null, CustomerType.Individual, "001'23", "a@example.com");

        Assert.Equal(id, customer.Id);
        Assert.Equal(" Name ", customer.Name);
        Assert.Null(customer.TradeName);
        Assert.Equal(CustomerType.Individual, customer.CustomerType);
        Assert.Equal("001'23", customer.TaxId);
        Assert.Equal("a@example.com", customer.Email);
    }

    [Fact]
    public void RejectsCorruptedPersistedCustomer()
    {
        Assert.Throws<ArgumentException>(() =>
            Customer.Reconstitute(Guid.NewGuid(), "Name", null, (CustomerType)99, "001", "a@example.com"));
    }

    [Fact] public void CreatesNormalizedCompany() {
        var customer = new Customer(" Acme ", " Shop ", CustomerType.Company, " 001 ", " a@example.com ");
        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Acme", customer.Name);
        Assert.Equal("Shop", customer.TradeName);
        Assert.Equal("001", customer.TaxId);
    }
    [Fact] public void CreatesIndividualWithBlankTradeName() {
        var customer = new Customer(" Name ", " ", CustomerType.Individual, "001", "a@example.com");
        Assert.Null(customer.TradeName);
    }
    [Theory]
    [InlineData("", "001", "a@example.com")]
    [InlineData("Name", " ", "a@example.com")]
    [InlineData("Name", "001", "invalid")]
    public void RejectsInvalidRequiredFields(string name, string taxId, string email) => Assert.Throws<ArgumentException>(() => new Customer(name, null, CustomerType.Individual, taxId, email));
    [Fact] public void RejectsTradeNameForIndividual() => Assert.Throws<ArgumentException>(() => new Customer("Name", "Shop", CustomerType.Individual, "123", "a@example.com"));
    [Fact] public void RejectsUndefinedType() => Assert.Throws<ArgumentException>(() => new Customer("Name", null, (CustomerType)99, "123", "a@example.com"));
}
