using BallastLane.Domain.Enums;

namespace BallastLane.Application.UseCases.Customers.ListCustomers;

public sealed record ListCustomersOutput(Guid Id, string Name, string? TradeName, CustomerType CustomerType, string TaxId, string Email, Guid Version, bool Active);
