using System.ComponentModel.DataAnnotations;
using BallastLane.Domain.Enums;
namespace BallastLane.WebApi.Models.Requests.Customers;
public sealed class CreateCustomerRequest
{
    /// <example>Acme Corporation</example>
    [Required] public string? Name { get; init; }
    /// <example>Acme</example>
    public string? TradeName { get; init; }
    /// <summary>Customer type: Individual or Company. Numeric values are not accepted.</summary>
    /// <example>Company</example>
    [Required] public CustomerType? CustomerType { get; init; }
    /// <example>00123456789</example>
    [Required] public string? TaxId { get; init; }
    /// <example>contact@acme.example</example>
    [Required] public string? Email { get; init; }
}
