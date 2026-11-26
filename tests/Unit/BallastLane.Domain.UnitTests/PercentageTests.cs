using BallastLane.Examples;
using Xunit;

namespace BallastLane.Domain.UnitTests;

public class PercentageTests
{
    [Theory]
    [InlineData(200, 25, 50)]
    [InlineData(0, 0, 0)]
    [InlineData(1, 100, 1)]
    public void Calculates(decimal value, decimal percentage, decimal expected) =>
        Assert.Equal(expected, Percentage.Calculate(value, percentage));

    [Fact]
    public void PreservesDecimalPrecision() => Assert.Equal(0.0333m, Percentage.Calculate(0.1m, 33.3m));

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public void RejectsInvalidInput(decimal value, decimal percentage) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Percentage.Calculate(value, percentage));
}
