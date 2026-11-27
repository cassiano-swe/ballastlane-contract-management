using BallastLane.Domain.Entities;

namespace BallastLane.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task CreateAsync(User user, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
}
