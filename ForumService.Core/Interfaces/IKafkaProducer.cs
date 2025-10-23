using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    public interface IKafkaProducer
    {
        Task ProduceAsync(string topic, string key, string value, CancellationToken cancellationToken = default);
        void BeginTransaction();
        void CommitTransaction();
        void AbortTransaction();
        void Flush(TimeSpan timeout);
        void Dispose();
    }
}