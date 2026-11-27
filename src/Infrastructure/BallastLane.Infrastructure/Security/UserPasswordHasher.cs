using System.Security.Cryptography;
using BallastLane.Application.Abstractions.Security;
using BallastLane.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BallastLane.Infrastructure.Security;

public sealed class UserPasswordHasher : IUserPasswordHasher
{
    private readonly PasswordHasher<User> hasher = new();
    private readonly string dummyHash;

    public UserPasswordHasher() => dummyHash = Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    // The stock PasswordHasher does not use the user argument. Provisioning precedes User construction.
    public string Hash(string password) => hasher.HashPassword(null!, password);

    public bool Verify(User user, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword(user, user.PasswordHash, password)
                is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Dummy verification has no account and uses the same stock implementation and work factor.
    public void VerifyDummy(string password) => hasher.VerifyHashedPassword(null!, dummyHash, password);
}
