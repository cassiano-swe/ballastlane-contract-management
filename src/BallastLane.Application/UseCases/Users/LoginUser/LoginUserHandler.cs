using BallastLane.Application.Abstractions.Persistence;
using BallastLane.Application.Abstractions.Security;
using BallastLane.Application.Exceptions;
using BallastLane.Domain.Entities;
using FluentValidation;

namespace BallastLane.Application.UseCases.Users.LoginUser;

public sealed class LoginUserHandler(
    LoginUserValidator validator, IUserRepository repository, IUserPasswordHasher passwordHasher, IAccessTokenIssuer tokenIssuer)
{
    public async Task<LoginUserOutput> HandleAsync(LoginUserInput input, CancellationToken cancellationToken)
    {
        var normalized = new LoginUserInput(input.Email is null ? null : User.NormalizeEmail(input.Email), input.Password);
        await validator.ValidateAndThrowAsync(normalized, cancellationToken);

        var user = await repository.GetByEmailAsync(normalized.Email!, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (user is null)
        {
            passwordHasher.VerifyDummy(normalized.Password!);
            throw new InvalidCredentialsException();
        }

        if (!passwordHasher.Verify(user, normalized.Password!) || !user.Active)
            throw new InvalidCredentialsException();

        cancellationToken.ThrowIfCancellationRequested();
        var token = tokenIssuer.Issue(user.Id);
        return new LoginUserOutput(token.Value, token.ExpiresIn);
    }
}
