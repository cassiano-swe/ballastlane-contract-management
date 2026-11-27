using System.Net.Mail;
using BallastLane.Domain.Enums;

namespace BallastLane.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; }
    public string Name { get; }
    public string? TradeName { get; }
    public CustomerType CustomerType { get; }
    public string TaxId { get; }
    public string Email { get; }

    public Customer(string? name, string? tradeName, CustomerType customerType, string? taxId, string? email)
    {
        name = name?.Trim(); tradeName = tradeName?.Trim(); taxId = taxId?.Trim(); email = email?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Invalid name.", nameof(name));
        if (!Enum.IsDefined(customerType)) throw new ArgumentException("Invalid customer type.", nameof(customerType));
        if (string.IsNullOrWhiteSpace(tradeName)) tradeName = null;
        if (tradeName?.Length > 200 || (tradeName is not null && customerType != CustomerType.Company)) throw new ArgumentException("Invalid trade name.", nameof(tradeName));
        if (string.IsNullOrWhiteSpace(taxId) || taxId.Length > 50) throw new ArgumentException("Invalid tax identifier.", nameof(taxId));
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email) throw new ArgumentException("Invalid email.", nameof(email));
        Id = Guid.NewGuid(); Name = name; TradeName = tradeName; CustomerType = customerType; TaxId = taxId; Email = email;
    }
}
