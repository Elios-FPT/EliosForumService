using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    public interface IKafkaProducerRepository<T> where T : class
    {
        Task<T> ProduceCreateAsync(T entity, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default);
        Task<T> ProduceUpdateAsync(T entity, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default);
        Task<Guid> ProduceDeleteAsync(Guid id, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> ProduceGetAllAsync(string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default);
        Task<T?> ProduceGetByIdAsync(Guid id, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default);
        void Dispose();
    }
}