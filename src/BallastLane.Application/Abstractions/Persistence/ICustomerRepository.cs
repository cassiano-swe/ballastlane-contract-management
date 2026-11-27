using BallastLane.Domain.Entities;
namespace BallastLane.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task CreateAsync(Customer customer, CancellationToken cancellationToken);
    Task<IReadOnlyList<Customer>> ListAsync(CancellationToken cancellationToken);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
