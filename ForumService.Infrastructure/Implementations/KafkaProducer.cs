using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class KafkaProducer : IKafkaProducer
    {
        private readonly IProducer<string, string> _producer;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IAdminClient _adminClient;
        private bool _topicsChecked = false;
        private readonly SemaphoreSlim _topicCheckSemaphore = new(1, 1);

        public KafkaProducer(IAppConfiguration appConfiguration)
        {
            _appConfiguration = appConfiguration;

            var config = new ProducerConfig
            {
                BootstrapServers = _appConfiguration.GetKafkaBootstrapServers(),
                BatchSize = 16384,
                LingerMs = 5,
                CompressionType = CompressionType.Gzip,
                MessageTimeoutMs = 30000,
                RetryBackoffMs = 100
            };

            _producer = new ProducerBuilder<string, string>(config).Build();

            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _appConfiguration.GetKafkaBootstrapServers()
            };
            _adminClient = new AdminClientBuilder(adminConfig).Build();
        }

        public async Task ProduceAsync(string topic, string key, string value, CancellationToken cancellationToken = default)
        {
            await EnsureTopicExistsAsync(topic, cancellationToken);

            await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = value
            }, cancellationToken);
        }

        private async Task EnsureTopicExistsAsync(string topic, CancellationToken cancellationToken = default)
        {
            await _topicCheckSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (_topicsChecked) return;

                var topicsMetadata = _adminClient.GetMetadata(TimeSpan.FromSeconds(10));
                var topicExists = topicsMetadata.Topics.Any(t => t.Topic == topic);

                if (!topicExists)
                {
                    await CreateTopicAsync(topic, cancellationToken);
                }

                _topicsChecked = true;
            }
            finally
            {
                _topicCheckSemaphore.Release();
            }
        }

        private async Task CreateTopicAsync(string topic, CancellationToken cancellationToken = default)
        {
            try
            {
                var topicSpecification = new TopicSpecification
                {
                    Name = topic,
                    NumPartitions = 3,
                    ReplicationFactor = 1,
                    Configs = new Dictionary<string, string>
                    {
                        { "cleanup.policy", "delete" },
                        { "retention.ms", "604800000" },
                        { "segment.ms", "86400000" }
                    }
                };

                await _adminClient.CreateTopicsAsync(
                    new[] { topicSpecification },
                    new CreateTopicsOptions
                    {
                        RequestTimeout = TimeSpan.FromSeconds(30),
                        OperationTimeout = TimeSpan.FromSeconds(30)
                    }
                );

                Console.WriteLine($"Topic '{topic}' created successfully.");
            }
            catch (CreateTopicsException e)
            {
                if (e.Results[0].Error.Code == ErrorCode.TopicAlreadyExists)
                {
                    Console.WriteLine($"Topic '{topic}' already exists.");
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Failed to create topic '{topic}': {e.Results[0].Error.Reason}", e);
                }
            }
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
            _topicCheckSemaphore?.Dispose();
            _adminClient?.Dispose();
            _producer?.Dispose();
        }
    }
}