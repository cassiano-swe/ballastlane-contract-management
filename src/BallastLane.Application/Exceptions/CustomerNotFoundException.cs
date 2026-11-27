namespace BallastLane.Application.Exceptions;

public sealed class CustomerNotFoundException : Exception
{
    public CustomerNotFoundException() : base("The requested customer was not found.") { }
}
