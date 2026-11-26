namespace BallastLane.Examples;

// Temporary architectural demonstration.
public static class Percentage
{
    public static decimal Calculate(decimal value, decimal percentage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (percentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
        return value * (percentage / 100m);
    }
}
