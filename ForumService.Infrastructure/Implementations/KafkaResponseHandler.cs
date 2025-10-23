using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class KafkaResponseHandler<T> : IKafkaResponseHandler<T> where T : class
    {
        private readonly IKafkaProducer _producer;
        private readonly string _dlqTopic;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IConfiguration _configuration;
        private readonly IAppConfiguration _appConfiguration;

        public KafkaResponseHandler(IConfiguration configuration, IAppConfiguration appConfiguration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultBufferSize = 4096
            };

            var bootstrapServers = _configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers configuration is missing.");

            _dlqTopic = $"{typeof(T).Name.ToLower()}-dlq"; 
            _producer = new KafkaProducer(_appConfiguration);
        }

        public async Task SendGetAllResponseAsync(string correlationId, IEnumerable<T> results, string responseTopic)
        {
            var wrapper = new EventWrapper
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "GET_ALL_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = results,
                CorrelationId = correlationId
            };

            var message = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await _producer.ProduceAsync(responseTopic, correlationId, message);
            Console.WriteLine($"Sent GET_ALL_RESPONSE for CorrelationId: {correlationId} to topic: {responseTopic}");
        }

        public async Task SendGetByIdResponseAsync(string correlationId, T? result, string responseTopic)
        {
            var wrapper = new EventWrapper
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "GET_BY_ID_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = result,
                CorrelationId = correlationId
            };

            var message = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await _producer.ProduceAsync(responseTopic, correlationId, message);
            Console.WriteLine($"Sent GET_BY_ID_RESPONSE for CorrelationId: {correlationId} to topic: {responseTopic}");
        }

        public async Task SendCreatedResponseAsync(T entity, string correlationId, string responseTopic)
        {
            var wrapper = new EventWrapper
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "CREATED_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = entity,
                CorrelationId = correlationId
            };

            var message = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await _producer.ProduceAsync(responseTopic, correlationId, message);
            Console.WriteLine($"Sent CREATED_RESPONSE for CorrelationId: {correlationId} to topic: {responseTopic}");
        }

        public async Task SendUpdatedResponseAsync(T entity, string correlationId, string responseTopic)
        {
            var wrapper = new EventWrapper
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "UPDATED_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = entity,
                CorrelationId = correlationId
            };

            var message = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await _producer.ProduceAsync(responseTopic, correlationId, message);
            Console.WriteLine($"Sent UPDATED_RESPONSE for CorrelationId: {correlationId} to topic: {responseTopic}");
        }

        public async Task SendDeletedResponseAsync(Guid id, string correlationId, string responseTopic)
        {
            var wrapper = new EventWrapper
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "DELETED_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = id,
                CorrelationId = correlationId
            };

            var message = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await _producer.ProduceAsync(responseTopic, correlationId, message);
            Console.WriteLine($"Sent DELETED_RESPONSE for CorrelationId: {correlationId} to topic: {responseTopic}");
        }

        public async Task SendErrorResponseAsync(EventWrapper wrapper, string errorMessage, string responseTopic)
        {
            var responseWrapper = new EventWrapper
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = "ERROR_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = errorMessage,
                CorrelationId = wrapper.CorrelationId
            };

            var message = JsonSerializer.Serialize(responseWrapper, _jsonOptions);
            await _producer.ProduceAsync(responseTopic, wrapper.CorrelationId, message);
            Console.WriteLine($"Sent ERROR_RESPONSE for CorrelationId: {wrapper.CorrelationId} to topic: {responseTopic}");
        }

        public async Task SendToDeadLetterQueueAsync(string message, Exception ex)
        {
            try
            {
                var dlqMessage = JsonSerializer.Serialize(new
                {
                    OriginalMessage = message,
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                }, _jsonOptions);
                await _producer.ProduceAsync(_dlqTopic, Guid.NewGuid().ToString(), dlqMessage);
                Console.WriteLine($"Sent to DLQ topic: {_dlqTopic}, Error: {ex.Message}");
            }
            catch (Exception dlqEx)
            {
                Console.WriteLine($"Failed to publish to DLQ topic {_dlqTopic}: {dlqEx.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                _producer?.Flush(TimeSpan.FromSeconds(5));
                _producer?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disposal: {ex.Message}");
            }
        }
    }
}