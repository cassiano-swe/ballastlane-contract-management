namespace BallastLane.Application.Exceptions;

public sealed class CustomerConcurrencyException() : Exception(DefaultMessage)
{
    public const string DefaultMessage =
        "The customer has changed since it was retrieved. Retrieve the latest customer state before attempting the operation again.";
}
