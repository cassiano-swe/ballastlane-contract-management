using BallastLane.Domain.Enums;

namespace BallastLane.Application.UseCases.Customers.GetCustomerById;

public sealed record GetCustomerByIdOutput(Guid Id, string Name, string? TradeName, CustomerType CustomerType, string TaxId, string Email, Guid Version, bool Active);
