using BallastLane.Application.UseCases.Examples;
using FluentValidation;
using Xunit;
namespace BallastLane.Application.UnitTests;
public class CalculatePercentageTests
{
    [Fact] public void ReturnsCalculatedAmount() => Assert.Equal(50m, new CalculatePercentageHandler(new()).Handle(new(200, 25)).Amount);
    [Theory][InlineData(-1, 1)][InlineData(1, -1)][InlineData(1, 101)]
    public void RejectsInvalidInput(decimal value, decimal percentage) => Assert.Throws<ValidationException>(() => new CalculatePercentageHandler(new()).Handle(new(value, percentage)));
}
