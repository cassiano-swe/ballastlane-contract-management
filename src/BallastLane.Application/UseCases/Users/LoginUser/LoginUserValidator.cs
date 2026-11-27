using System.Net.Mail;
using FluentValidation;

namespace BallastLane.Application.UseCases.Users.LoginUser;

public sealed class LoginUserValidator : AbstractValidator<LoginUserInput>
{
    public LoginUserValidator()
    {
        RuleFor(input => input.Email).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(254).WithMessage("Email must be at most 254 characters.")
            .Must(email => email is not null && MailAddress.TryCreate(email, out var address) && address.Address == email)
            .WithMessage("A valid email is required.");
        RuleFor(input => input.Password).Cascade(CascadeMode.Stop)
            .Must(password => !string.IsNullOrEmpty(password)).WithMessage("Password is required.")
            .MaximumLength(1024).WithMessage("Password must be at most 1024 characters.");
    }
}
