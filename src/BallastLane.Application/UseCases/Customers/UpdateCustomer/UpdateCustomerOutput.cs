using BallastLane.Domain.Enums;

namespace BallastLane.Application.UseCases.Customers.UpdateCustomer;

public sealed record UpdateCustomerOutput(
    Guid Id, string Name, string? TradeName, CustomerType CustomerType, string TaxId, string Email, Guid Version);
