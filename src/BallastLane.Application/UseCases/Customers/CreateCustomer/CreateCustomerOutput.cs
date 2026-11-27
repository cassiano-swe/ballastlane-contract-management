using BallastLane.Domain.Enums;
namespace BallastLane.Application.UseCases.Customers.CreateCustomer;
public sealed record CreateCustomerOutput(Guid Id, string Name, string? TradeName, CustomerType CustomerType, string TaxId, string Email);
