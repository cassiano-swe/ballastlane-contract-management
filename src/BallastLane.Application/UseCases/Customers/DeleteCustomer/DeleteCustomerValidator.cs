using FluentValidation;

namespace BallastLane.Application.UseCases.Customers.DeleteCustomer;

public sealed class DeleteCustomerValidator : AbstractValidator<DeleteCustomerInput>
{
    public DeleteCustomerValidator()
    {
        RuleFor(input => input.Id).NotEmpty().WithMessage("The customer identifier must not be empty.");
        RuleFor(input => input.Version).NotEmpty().WithMessage("The customer version must not be empty.");
    }
}
