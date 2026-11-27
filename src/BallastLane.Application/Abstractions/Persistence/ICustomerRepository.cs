using BallastLane.Domain.Entities;
namespace BallastLane.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task CreateAsync(Customer customer, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Customer customer, Guid expectedVersion, CancellationToken cancellationToken);
    Task<bool> DeactivateAsync(Guid id, Guid expectedVersion, Guid newVersion, CancellationToken cancellationToken);
}
