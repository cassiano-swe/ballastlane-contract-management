using System.Net.Mail;

namespace BallastLane.Domain.Entities;

public sealed class User
{
    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string PasswordHash { get; }
    public bool Active { get; }

    public User(string? name, string? email, string? passwordHash)
        : this(Guid.NewGuid(), name?.Trim(), email is null ? null : NormalizeEmail(email), passwordHash, true)
    {
    }

    public static User Reconstitute(Guid id, string name, string email, string passwordHash, bool active) =>
        new(id, name, email, passwordHash, active);

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private User(Guid id, string? name, string? email, string? passwordHash, bool active)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Invalid user identifier.", nameof(id));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new ArgumentException("Invalid user name.", nameof(name));
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || email != NormalizeEmail(email)
            || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new ArgumentException("Invalid user email.", nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("A password hash is required.", nameof(passwordHash));

        Id = id;
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Active = active;
    }
}
