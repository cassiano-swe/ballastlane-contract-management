using BallastLane.Domain.Entities;

namespace BallastLane.Application.Abstractions.Security;

public interface IUserPasswordHasher
{
    string Hash(string password);
    bool Verify(User user, string password);
    void VerifyDummy(string password);
}
