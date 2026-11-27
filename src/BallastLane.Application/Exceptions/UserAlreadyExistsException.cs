namespace BallastLane.Application.Exceptions;

public sealed class UserAlreadyExistsException()
    : Exception("A user with the provided email already exists.");
