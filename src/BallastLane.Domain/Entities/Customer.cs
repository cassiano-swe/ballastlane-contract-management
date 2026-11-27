using System.Diagnostics.CodeAnalysis;
using System.Net.Mail;
using BallastLane.Domain.Enums;

namespace BallastLane.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; }
    public string Name { get; private set; }
    public string? TradeName { get; private set; }
    public CustomerType CustomerType { get; private set; }
    public string TaxId { get; private set; }
    public string Email { get; private set; }
    public Guid Version { get; private set; }
    public bool Active { get; private set; }

    public Customer(string? name, string? tradeName, CustomerType customerType, string? taxId, string? email)
        : this(Guid.NewGuid(), name?.Trim(), string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim(),
            customerType, taxId?.Trim(), email?.Trim(), Guid.NewGuid(), true)
    {
    }

    public static Customer Reconstitute(Guid id, string name, string? tradeName, CustomerType customerType, string taxId, string email, Guid version, bool active)
        => new(id, name, tradeName, customerType, taxId, email, version, active);

    private Customer(Guid id, string? name, string? tradeName, CustomerType customerType, string? taxId, string? email, Guid version, bool active)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invalid customer identifier.", nameof(id));
        if (version == Guid.Empty) throw new ArgumentException("Invalid customer version.", nameof(version));
        Validate(name, tradeName, customerType, taxId, email);
        Id = id;
        Name = name;
        TradeName = tradeName;
        CustomerType = customerType;
        TaxId = taxId;
        Email = email;
        Version = version;
        Active = active;
    }

    public void Update(string? name, string? tradeName, CustomerType customerType, string? taxId, string? email)
    {
        name = name?.Trim();
        tradeName = string.IsNullOrWhiteSpace(tradeName) ? null : tradeName.Trim();
        taxId = taxId?.Trim();
        email = email?.Trim();
        Validate(name, tradeName, customerType, taxId, email);

        Name = name;
        TradeName = tradeName;
        CustomerType = customerType;
        TaxId = taxId;
        Email = email;
        RenewVersion();
    }

    public void Deactivate()
    {
        if (!Active)
            return;

        Active = false;
        RenewVersion();
    }

    private void RenewVersion()
    {
        Guid newVersion;
        do
        {
            newVersion = Guid.NewGuid();
        } while (newVersion == Guid.Empty || newVersion == Version);

        Version = newVersion;
    }

    private static void Validate(
        [NotNull] string? name,
        string? tradeName,
        CustomerType customerType,
        [NotNull] string? taxId,
        [NotNull] string? email)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200) throw new ArgumentException("Invalid name.", nameof(name));
        if (!Enum.IsDefined(customerType)) throw new ArgumentException("Invalid customer type.", nameof(customerType));
        if (tradeName is not null && (string.IsNullOrWhiteSpace(tradeName) || tradeName.Length > 200 || customerType != CustomerType.Company))
            throw new ArgumentException("Invalid trade name.", nameof(tradeName));
        if (string.IsNullOrWhiteSpace(taxId) || taxId.Length > 50) throw new ArgumentException("Invalid tax identifier.", nameof(taxId));
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email) throw new ArgumentException("Invalid email.", nameof(email));
    }
}
