using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Abstractions.Security;
using BallastLane.Application.Exceptions;
using BallastLane.Application.UseCases.Users.LoginUser;
using BallastLane.Domain.Entities;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace BallastLane.Application.UnitTests;

public sealed class LoginUserTests
{
    private const string Email = "user@example.com";
    private const string Password = "  example password  ";
    private readonly IUserRepository repository = Substitute.For<IUserRepository>();
    private readonly IUserPasswordHasher hasher = Substitute.For<IUserPasswordHasher>();
    private readonly IAccessTokenIssuer issuer = Substitute.For<IAccessTokenIssuer>();
    private LoginUserHandler Handler => new(new LoginUserValidator(), repository, hasher, issuer);

    [Fact]
    public async Task AuthenticatesWithNormalizedEmailAndExactPassword()
    {
        using var source = new CancellationTokenSource();
        var user = new User("User", Email, "synthetic");
        repository.GetByEmailAsync(Email, source.Token).Returns(user);
        hasher.Verify(user, Password).Returns(true);
        issuer.Issue(user.Id).Returns(new AccessToken("synthetic-token", 1800));

        var result = await Handler.HandleAsync(new(" USER@EXAMPLE.COM ", Password), source.Token);

        Assert.Equal(("synthetic-token", "Bearer", 1800), (result.AccessToken, result.TokenType, result.ExpiresIn));
        await repository.Received(1).GetByEmailAsync(Email, source.Token);
        hasher.Received(1).Verify(user, Password);
        issuer.Received(1).Issue(user.Id);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("wrong-password")]
    public async Task RejectsCredentialsWithoutIssuingToken(string scenario)
    {
        User? user = scenario == "missing" ? null
            : User.Reconstitute(Guid.NewGuid(), "User", Email, "synthetic", active: scenario != "inactive");
        repository.GetByEmailAsync(Email, default).Returns(user);
        if (user is not null)
            hasher.Verify(user, Password).Returns(scenario == "inactive");

        var error = await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => Handler.HandleAsync(new(Email, Password), default));

        Assert.Equal("Invalid email or password.", error.Message);
        Assert.Empty(issuer.ReceivedCalls());
        if (user is null)
            hasher.Received(1).VerifyDummy(Password);
        else
            hasher.Received(1).Verify(user, Password);
        Assert.Single(hasher.ReceivedCalls());
    }

    [Theory]
    [InlineData(null, Password)]
    [InlineData("not an email", Password)]
    [InlineData(Email, null)]
    [InlineData(Email, "")]
    public async Task RejectsInvalidInputsBeforeRepositoryLookup(string? email, string? password)
    {
        await Assert.ThrowsAsync<ValidationException>(() => Handler.HandleAsync(new(email, password), default));
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task RejectsOversizeInputsWithoutEchoingThem()
    {
        var email = new string('a', 245) + "@example.com";
        var password = new string('x', 1025);
        var error = await Assert.ThrowsAsync<ValidationException>(() => Handler.HandleAsync(new(email, password), default));
        Assert.Equal(2, error.Errors.Count());
        Assert.DoesNotContain(email, error.Message);
        Assert.DoesNotContain(password, error.Message);
        Assert.Empty(repository.ReceivedCalls());
    }

    [Fact]
    public async Task PropagatesCancellationAndPersistenceFailure()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Handler.HandleAsync(new(Email, Password), source.Token));
        var failure = new InvalidOperationException("repository failure");
        repository.GetByEmailAsync(Email, default).Returns(Task.FromException<User?>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(
            () => Handler.HandleAsync(new(Email, Password), default)));
        Assert.Empty(hasher.ReceivedCalls());
        Assert.Empty(issuer.ReceivedCalls());
    }
}
