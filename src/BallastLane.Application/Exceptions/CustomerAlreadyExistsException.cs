namespace BallastLane.Application.Exceptions;
public sealed class CustomerAlreadyExistsException : Exception
{
    public CustomerAlreadyExistsException() : base("A customer with the provided tax identifier already exists.") { }
}
