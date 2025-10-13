using Confluent.Kafka;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class KafkaProducer : IKafkaProducer
    {
        private readonly IProducer<string, string> _producer;
        private readonly IAppConfiguration _appConfiguration;    

        public KafkaProducer(IAppConfiguration appConfiguration)
        {
            _appConfiguration = appConfiguration;

            var config = new ProducerConfig { BootstrapServers = _appConfiguration.GetKafkaBootstrapServers() };
            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task ProduceAsync(string topic, string key, string value)
        {
            await _producer.ProduceAsync(topic, new Message<string, string> { Key = key, Value = value });
        }

        public void BeginTransaction()
        {
            _producer.InitTransactions(TimeSpan.FromSeconds(60));
            _producer.BeginTransaction();
        }

        public void CommitTransaction()
        {
            _producer.CommitTransaction();
        }

        public void AbortTransaction()
        {
            _producer.AbortTransaction();
        }

        public void Flush(TimeSpan timeout)
        {
            _producer.Flush(timeout);
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}
