using FluentValidation;

namespace BallastLane.Application.UseCases.Customers.GetCustomerById;

public sealed class GetCustomerByIdValidator : AbstractValidator<GetCustomerByIdInput>
{
    public GetCustomerByIdValidator()
    {
        RuleFor(input => input.Id).NotEmpty().WithMessage("The customer identifier must not be empty.");
    }
}
