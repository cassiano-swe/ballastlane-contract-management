using BallastLane.Examples;
using FluentValidation;

namespace BallastLane.Application.UseCases.Examples;

// Temporary architectural demonstration.
public sealed record CalculatePercentageInput(decimal Value, decimal Percentage);
public sealed record CalculatePercentageOutput(decimal Amount);

public sealed class CalculatePercentageValidator : AbstractValidator<CalculatePercentageInput>
{
    public CalculatePercentageValidator()
    {
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Percentage).InclusiveBetween(0, 100);
    }
}

public sealed class CalculatePercentageHandler(CalculatePercentageValidator validator)
{
    public CalculatePercentageOutput Handle(CalculatePercentageInput input)
    {
        validator.ValidateAndThrow(input);
        return new(Percentage.Calculate(input.Value, input.Percentage));
    }
}
