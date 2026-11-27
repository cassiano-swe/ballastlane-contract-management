using System.ComponentModel.DataAnnotations;
using BallastLane.Domain.Enums;

namespace BallastLane.WebApi.Models.Requests.Customers;

public sealed class UpdateCustomerRequest
{
    [Required] public string? Name { get; init; }
    public string? TradeName { get; init; }
    [Required] public CustomerType? CustomerType { get; init; }
    [Required] public string? TaxId { get; init; }
    [Required] public string? Email { get; init; }
    [Required] public Guid? Version { get; init; }
}
