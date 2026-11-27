using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using Xunit;

namespace BallastLane.Domain.UnitTests;

public class CustomerDeactivationTests
{
    [Fact]
    public void DeactivatesOnceAndPreservesBusinessState()
    {
        var customer = new Customer("Acme", "Shop", CustomerType.Company, "001", "a@example.com");
        var state = BusinessState(customer);
        var original = customer.Version;
        Assert.True(customer.Active);

        customer.Deactivate();

        Assert.False(customer.Active);
        Assert.NotEqual(Guid.Empty, customer.Version);
        Assert.NotEqual(original, customer.Version);
        Assert.Equal(state, BusinessState(customer));
        var inactiveVersion = customer.Version;

        customer.Deactivate();

        Assert.False(customer.Active);
        Assert.Equal(inactiveVersion, customer.Version);
        Assert.Equal(state, BusinessState(customer));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReconstitutionAndUpdatePreserveActivation(bool active)
    {
        var id = Guid.NewGuid();
        var version = Guid.NewGuid();
        var customer = Customer.Reconstitute(id, "Acme", null, CustomerType.Company, "001", "a@example.com", version, active);
        Assert.Equal((id, version, active), (customer.Id, customer.Version, customer.Active));

        customer.Update("Updated", null, CustomerType.Company, "001", "a@example.com");

        Assert.Equal(active, customer.Active);
        Assert.NotEqual(version, customer.Version);
    }

    private static object BusinessState(Customer customer) =>
        (customer.Id, customer.Name, customer.TradeName, customer.CustomerType, customer.TaxId, customer.Email);
}
