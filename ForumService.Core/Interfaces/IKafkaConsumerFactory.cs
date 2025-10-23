using ForumService.Core.Interfaces;

namespace ForumService.Infrastructure.Kafka
{
    public interface IKafkaConsumerFactory<T> where T : class
    {
        IKafkaConsumerRepository<T> CreateConsumer(string sourceServiceName);
    }
}