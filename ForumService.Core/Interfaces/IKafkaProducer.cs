using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    public interface IKafkaProducer : IDisposable
    {
        Task ProduceAsync(string topic, string key, string value);
        void BeginTransaction();
        void CommitTransaction();
        void AbortTransaction();
        void Flush(TimeSpan timeout);
    }
}
