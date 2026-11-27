using BallastLane.Domain.Entities;
namespace BallastLane.Application.Abstractions.Persistence;
public interface ICustomerRepository { Task CreateAsync(Customer customer, CancellationToken cancellationToken); }
