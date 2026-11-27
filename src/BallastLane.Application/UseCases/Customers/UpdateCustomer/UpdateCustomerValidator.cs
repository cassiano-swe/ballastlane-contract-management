using System.Net.Mail;
using BallastLane.Domain.Enums;
using FluentValidation;

namespace BallastLane.Application.UseCases.Customers.UpdateCustomer;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerInput>
{
    public UpdateCustomerValidator()
    {
        RuleFor(input => input.Id).NotEmpty().WithMessage("The customer identifier must not be empty.");
        RuleFor(input => input.Version).NotEmpty().WithMessage("The customer version must not be empty.");
        RuleFor(input => input.Name).NotEmpty().MaximumLength(200);
        RuleFor(input => input.TradeName).MaximumLength(200)
            .Must((input, value) => string.IsNullOrEmpty(value) || input.CustomerType == CustomerType.Company)
            .WithMessage("Trade name is only allowed for companies.");
        RuleFor(input => input.CustomerType).IsInEnum();
        RuleFor(input => input.TaxId).NotEmpty().MaximumLength(50);
        RuleFor(input => input.Email).NotEmpty().MaximumLength(254)
            .Must(value => value is not null && MailAddress.TryCreate(value, out var address) && address.Address == value)
            .WithMessage("A valid email is required.");
    }
}
