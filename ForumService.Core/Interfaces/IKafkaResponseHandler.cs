using ForumService.Core.Models;

namespace ForumService.Core.Interfaces
{
    public interface IKafkaResponseHandler<T> where T : class
    {
        Task SendGetAllResponseAsync(string correlationId, IEnumerable<T> results, string responseTopic);
        Task SendGetByIdResponseAsync(string correlationId, T? result, string responseTopic);
        Task SendCreatedResponseAsync(T entity, string correlationId, string responseTopic);
        Task SendUpdatedResponseAsync(T entity, string correlationId, string responseTopic);
        Task SendDeletedResponseAsync(Guid id, string correlationId, string responseTopic);
        Task SendErrorResponseAsync(EventWrapper wrapper, string errorMessage, string responseTopic);
        Task SendToDeadLetterQueueAsync(string message, Exception ex);
    }
}