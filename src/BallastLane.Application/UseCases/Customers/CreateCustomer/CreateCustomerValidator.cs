using System.Net.Mail;
using BallastLane.Domain.Enums;
using FluentValidation;
namespace BallastLane.Application.UseCases.Customers.CreateCustomer;
public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerInput>
{
    public CreateCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TradeName).MaximumLength(200).Must((x, value) => string.IsNullOrEmpty(value) || x.CustomerType == CustomerType.Company).WithMessage("Trade name is only allowed for companies.");
        RuleFor(x => x.CustomerType).IsInEnum();
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).Must(value => value is not null && MailAddress.TryCreate(value, out var address) && address.Address == value).WithMessage("A valid email is required.");
    }
}
