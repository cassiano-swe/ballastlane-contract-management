using BallastLane.Domain.Entities;
namespace BallastLane.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task CreateAsync(Customer customer, Guid authorId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Customer customer, Guid expectedVersion, Guid authorId, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, Guid expectedVersion, Guid newVersion, Guid authorId, CancellationToken cancellationToken);
}
