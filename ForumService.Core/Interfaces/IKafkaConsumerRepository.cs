using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    /// <summary>
    /// Interface for Kafka consumer repository handling message consumption.
    /// </summary>
    public interface IKafkaConsumerRepository<T> where T : class
    {
        Task StartConsumingAsync(CancellationToken cancellationToken = default);
        void Dispose();
    }
}
