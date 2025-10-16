namespace ForumService.Core.Interfaces;

/// <summary>
/// Interface for Kafka producer repository handling message production.
/// </summary>
public interface IKafkaProducerRepository<T> where T : class
{
    Task<string> ProduceCreateAsync(T entity, string? correlationId = null, CancellationToken cancellationToken = default);
    Task<string> ProduceUpdateAsync(T entity, string? correlationId = null, CancellationToken cancellationToken = default);
    Task<string> ProduceDeleteAsync(Guid id, string? correlationId = null, CancellationToken cancellationToken = default);
    Task ProduceGetAllAsync(string correlationId, CancellationToken cancellationToken = default);
    Task ProduceGetByIdAsync(Guid id, string correlationId, CancellationToken cancellationToken = default);
    void Dispose();
}