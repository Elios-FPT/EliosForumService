using Confluent.Kafka;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class KafkaProducerRepository<T> : IKafkaProducerRepository<T>, IDisposable where T : class
    {
        private readonly KafkaProducer _kafkaProducer;
        private readonly string _currentServiceName;
        private readonly JsonSerializerOptions _jsonOptions;
        private bool _disposed;

        private const string CREATE_EVENT = "CREATE";
        private const string UPDATE_EVENT = "UPDATE";
        private const string DELETE_EVENT = "DELETE";
        private const string GET_ALL_EVENT = "GET_ALL";
        private const string GET_BY_ID_EVENT = "GET_BY_ID";

        public KafkaProducerRepository(IAppConfiguration appConfiguration)
        {
            _kafkaProducer = new KafkaProducer(appConfiguration);
            _currentServiceName = appConfiguration.GetCurrentServiceName();

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultBufferSize = 4096,
                WriteIndented = true
            };
        }

        public async Task<T> ProduceCreateAsync(T entity, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            var corrId = await ProduceAsync(CREATE_EVENT, entity, destinationServiceName, correlationId, cancellationToken);
            return await KafkaResponseConsumer.WaitForResponseAsync<T>(corrId, responseTopic, cancellationToken);
        }

        public async Task<T> ProduceUpdateAsync(T entity, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            var corrId = await ProduceAsync(UPDATE_EVENT, entity, destinationServiceName, correlationId, cancellationToken);
            return await KafkaResponseConsumer.WaitForResponseAsync<T>(corrId, responseTopic, cancellationToken);
        }

        public async Task<Guid> ProduceDeleteAsync(Guid id, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            var corrId = await ProduceAsync(DELETE_EVENT, id.ToString(), destinationServiceName, correlationId, cancellationToken);
            var deletedIdStr = await KafkaResponseConsumer.WaitForResponseAsync<string>(corrId, responseTopic, cancellationToken);
            return Guid.Parse(deletedIdStr);
        }

        public async Task<IEnumerable<T>> ProduceGetAllAsync(string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            var corrId = await ProduceAsync(GET_ALL_EVENT, null, destinationServiceName, correlationId, cancellationToken);
            return await KafkaResponseConsumer.WaitForResponseAsync<IEnumerable<T>>(corrId, responseTopic, cancellationToken);
        }

        public async Task<T?> ProduceGetByIdAsync(Guid id, string destinationServiceName, string responseTopic, string? correlationId = null, CancellationToken cancellationToken = default)
        {
            var corrId = await ProduceAsync(GET_BY_ID_EVENT, id.ToString(), destinationServiceName, correlationId, cancellationToken);
            return await KafkaResponseConsumer.WaitForResponseAsync<T?>(corrId, responseTopic, cancellationToken);
        }

        private async Task<string> ProduceAsync(string eventType, object? payload, string destinationServiceName, string? correlationId, CancellationToken cancellationToken)
        {
            var usedCorrelationId = correlationId ?? Guid.NewGuid().ToString();
            var topic = $"{_currentServiceName}-{destinationServiceName}-{typeof(T).Name.ToLower()}";

            var eventWrapper = new EventWrapper(
                EventType: eventType,
                ModelType: typeof(T).Name,
                Payload: payload,
                EventId: Guid.NewGuid().ToString(),
                CorrelationId: usedCorrelationId,
                Timestamp: DateTime.UtcNow
            );

            var messageValue = JsonSerializer.Serialize(eventWrapper, _jsonOptions);
            await _kafkaProducer.ProduceAsync(topic, usedCorrelationId, messageValue, cancellationToken);

            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [{_currentServiceName}] Sent {eventType} | CorrelationId: {usedCorrelationId} to topic: {topic}");
            return usedCorrelationId;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _kafkaProducer?.Dispose();
                _disposed = true;
            }
        }
    }
}