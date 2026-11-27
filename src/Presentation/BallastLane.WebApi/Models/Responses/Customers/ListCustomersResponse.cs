using BallastLane.Domain.Enums;

namespace BallastLane.WebApi.Models.Responses.Customers;

public sealed record ListCustomersResponse(Guid Id, string Name, string? TradeName, CustomerType CustomerType, string TaxId, string Email);
