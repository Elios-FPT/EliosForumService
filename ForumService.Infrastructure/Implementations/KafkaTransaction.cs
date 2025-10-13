using Confluent.Kafka;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class KafkaTransaction : IKafkaTransaction
    {
        public IKafkaProducer Producer { get; }
        public IAppConfiguration _appConfiguration { get; }

        public KafkaTransaction(IAppConfiguration appConfiguration)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            var config = new ProducerConfig
            {
                BootstrapServers = _appConfiguration.GetKafkaBootstrapServers(),
                TransactionalId = Guid.NewGuid().ToString()
            };
            Producer = new KafkaProducer(_appConfiguration);
            Producer.BeginTransaction();
        }

        public void Dispose()
        {
            Producer?.Dispose();
        }
    }
}
