using BallastLane.Domain.Entities;
using Xunit;

namespace BallastLane.Domain.UnitTests;

public class UserTests
{
    private const string SyntheticHash = "  synthetic-test-hash\t";

    [Fact]
    public void CreationAssignsIdentityAndNormalizesOnlyNameAndEmail()
    {
        var user = new User("  Example Person  ", "  Name+Alias@Example.COM  ", SyntheticHash);
        var other = new User("Other", "other@example.com", SyntheticHash);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.NotEqual(other.Id, user.Id);
        Assert.Equal("Example Person", user.Name);
        Assert.Equal("name+alias@example.com", user.Email);
        Assert.Equal(SyntheticHash, user.PasswordHash);
        Assert.True(user.Active);
        Assert.DoesNotContain(SyntheticHash, user.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void RejectsMissingRequiredFields(string? missing)
    {
        Assert.Throws<ArgumentException>("name", () => new User(missing, "user@example.com", SyntheticHash));
        Assert.Throws<ArgumentException>("email", () => new User("Example", missing, SyntheticHash));
        Assert.Throws<ArgumentException>("passwordHash", () => new User("Example", "user@example.com", missing));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("Example <user@example.com>")]
    public void RejectsInvalidEmail(string email)
    {
        var exception = Assert.Throws<ArgumentException>("email", () => new User("Example", email, SyntheticHash));
        Assert.DoesNotContain(SyntheticHash, exception.Message);
        Assert.DoesNotContain(email, exception.Message);
    }

    [Fact]
    public void EnforcesLengthsAfterNormalization()
    {
        var name = new string('N', 200);
        var email = $"{new string('a', 64)}@{new string('b', 63)}.{new string('c', 63)}.{new string('d', 57)}.com";
        Assert.Equal(254, email.Length);
        var user = new User($" {name} ", $" {email} ", SyntheticHash);
        Assert.Equal(name, user.Name);
        Assert.Equal(email, user.Email);
        Assert.Throws<ArgumentException>("name", () => new User(name + "N", email, SyntheticHash));
        Assert.Throws<ArgumentException>("email", () => new User(name, email + "m", SyntheticHash));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReconstitutionPreservesPersistedValues(bool active)
    {
        var id = Guid.NewGuid();
        var user = User.Reconstitute(id, " Persisted Name ", "user@example.com", SyntheticHash, active);

        Assert.Equal((id, " Persisted Name ", "user@example.com", SyntheticHash, active),
            (user.Id, user.Name, user.Email, user.PasswordHash, user.Active));
    }

    [Fact]
    public void ReconstitutionRejectsInvalidPersistedState()
    {
        Assert.Throws<ArgumentException>("id", () =>
            User.Reconstitute(Guid.Empty, "Example", "user@example.com", SyntheticHash, true));
        Assert.Throws<ArgumentException>("email", () =>
            User.Reconstitute(Guid.NewGuid(), "Example", "USER@example.com", SyntheticHash, true));
        Assert.Throws<ArgumentException>("passwordHash", () =>
            User.Reconstitute(Guid.NewGuid(), "Example", "user@example.com", " ", true));
    }
}
