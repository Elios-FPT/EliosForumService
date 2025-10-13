using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using ForumService.Core.Interfaces;
using ForumService.Infrastructure.Models.Kafka;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ForumService.Infrastructure.Implementations;

namespace SUtility.Infrastructure.Implementations
{
    public class KafkaRepository<T> : IKafkaRepository<T> where T : class
    {
        private readonly IKafkaProducer _producer;
        private readonly IConsumer<string, string> _consumer;
        private readonly string _commandTopic;
        private readonly string _responseTopic;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IGenericRepository<T> _innerRepository;
        private readonly IConfiguration _configuration;
        private readonly ConcurrentBag<string> _processedEventIds;
        private readonly IAppConfiguration _appConfiguration;

        public KafkaRepository(IConfiguration configuration, IGenericRepository<T> innerRepository, IAppConfiguration appConfiguration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
            _processedEventIds = new ConcurrentBag<string>();
            _appConfiguration = appConfiguration;

            var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured.");

            _commandTopic = typeof(T).Name.ToLower();
            _responseTopic = $"{_commandTopic}-response";

            _producer = new KafkaProducer(_appConfiguration);

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = $"{_commandTopic}-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };
            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            _consumer.Subscribe(_commandTopic);
        }

        public async Task AddAsync(T entity)
        {
            await _innerRepository.AddAsync(entity);
            await PublishEvent("CREATE", entity, _commandTopic);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _innerRepository.AddRangeAsync(entities);
            foreach (var entity in entities)
            {
                await PublishEvent("CREATE", entity, _commandTopic);
            }
        }

        public async Task UpdateAsync(T entity)
        {
            await _innerRepository.UpdateAsync(entity);
            await PublishEvent("UPDATE", entity, _commandTopic);
        }

        public async Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            await _innerRepository.UpdateRangeAsync(entities);
            foreach (var entity in entities)
            {
                await PublishEvent("UPDATE", entity, _commandTopic);
            }
        }

        public async Task DeleteAsync(T entity)
        {
            await _innerRepository.DeleteAsync(entity);
            var id = GetEntityId(entity);
            await PublishEvent("DELETE", id, _commandTopic);
        }

        public async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            await _innerRepository.DeleteRangeAsync(entities);
            foreach (var entity in entities)
            {
                var id = GetEntityId(entity);
                await PublishEvent("DELETE", id, _commandTopic);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _innerRepository.SaveChangesAsync();
            _producer.Flush(TimeSpan.FromSeconds(60));
        }

        public async Task<ICombinedTransaction> BeginTransactionAsync()
        {
            var dbTransaction = await _innerRepository.BeginTransactionAsync();
            var kafkaTransaction = new KafkaTransaction(_appConfiguration);
            return new CombinedTransaction(kafkaTransaction, dbTransaction);
        }

        public async Task<T?> GetByIdAsync(Guid id)
        {
            return await _innerRepository.GetByIdAsync(id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _innerRepository.GetListAsync();
        }

        public async Task<int> GetCountAsync()
        {
            return await _innerRepository.GetCountAsync();
        }

        public async Task StartConsumingAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(cancellationToken);
                        if (consumeResult?.Message == null) continue;

                        var wrapper = JsonSerializer.Deserialize<EventWrapper>(consumeResult.Message.Value, _jsonOptions);
                        if (wrapper?.ModelType != typeof(T).Name) continue;

                        if (_processedEventIds.Contains(wrapper.EventId))
                        {
                            _consumer.Commit(consumeResult);
                            continue;
                        }

                        switch (wrapper.EventType)
                        {
                            case "CREATE":
                                var createEntity = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(wrapper.Payload), _jsonOptions);
                                await HandleCreateAsync(createEntity);
                                break;
                            case "UPDATE":
                                var updateEntity = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(wrapper.Payload), _jsonOptions);
                                await HandleUpdateAsync(updateEntity);
                                break;
                            case "DELETE":
                                await HandleDeleteAsync(Guid.Parse(wrapper.Payload.ToString()));
                                break;
                            case "GET_ALL":
                                await HandleGetAllAsync(wrapper.EventId);
                                break;
                            case "GET_BY_ID":
                                await HandleGetByIdAsync(Guid.Parse(wrapper.Payload.ToString()), wrapper.EventId);
                                break;
                        }

                        _processedEventIds.Add(wrapper.EventId);
                        _consumer.Commit(consumeResult);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        await PublishToDeadLetterQueue(ex.Message, ex);
                    }
                }
            }, cancellationToken);
        }

        private async Task PublishEvent(string eventType, object payload, string topic)
        {
            var wrapper = new EventWrapper
            {
                EventType = eventType,
                ModelType = typeof(T).Name,
                Payload = payload
            };

            var message = JsonSerializer.Serialize(wrapper, _jsonOptions);
            await _producer.ProduceAsync(topic, wrapper.EventId, message);
        }

        private string GetEntityId(T entity)
        {
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty == null)
                throw new InvalidOperationException("Entity must have an Id property.");
            return idProperty.GetValue(entity)?.ToString() ?? throw new InvalidOperationException("Entity Id cannot be null.");
        }

        private async Task HandleGetAllAsync(string eventId)
        {
            var results = await _innerRepository.GetListAsync();
            var responseWrapper = new EventWrapper
            {
                EventId = eventId,
                EventType = "GET_ALL_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = results
            };

            var message = JsonSerializer.Serialize(responseWrapper, _jsonOptions);
            await _producer.ProduceAsync(_responseTopic, eventId, message);
        }

        private async Task HandleGetByIdAsync(Guid id, string eventId)
        {
            var result = await _innerRepository.GetByIdAsync(id);
            var responseWrapper = new EventWrapper
            {
                EventId = eventId,
                EventType = "GET_BY_ID_RESPONSE",
                ModelType = typeof(T).Name,
                Payload = result
            };

            var message = JsonSerializer.Serialize(responseWrapper, _jsonOptions);
            await _producer.ProduceAsync(_responseTopic, eventId, message);
        }

        private async Task HandleCreateAsync(T entity)
        {
            await _innerRepository.AddAsync(entity);
            await _innerRepository.SaveChangesAsync();
            await PublishEvent("CREATED", entity, _responseTopic);
        }

        private async Task HandleUpdateAsync(T entity)
        {
            await _innerRepository.UpdateAsync(entity);
            await _innerRepository.SaveChangesAsync();
            await PublishEvent("UPDATED", entity, _responseTopic);
        }

        private async Task HandleDeleteAsync(Guid id)
        {
            var entity = await _innerRepository.GetByIdAsync(id);
            if (entity != null)
            {
                await _innerRepository.DeleteAsync(entity);
                await _innerRepository.SaveChangesAsync();
                await PublishEvent("DELETED", id, _responseTopic);
            }
        }

        private async Task PublishToDeadLetterQueue(string message, Exception ex)
        {
            try
            {
                var dlqMessage = JsonSerializer.Serialize(new { OriginalMessage = message, Error = ex.Message });
                await _producer.ProduceAsync($"{_commandTopic}-dlq", Guid.NewGuid().ToString(), dlqMessage);
            }
            catch (Exception dlqEx)
            {
                Console.WriteLine($"Failed to publish to DLQ: {dlqEx.Message}");
            }
        }

        public void Dispose()
        {
            _producer?.Flush(TimeSpan.FromSeconds(60));
            _producer?.Dispose();
            _consumer?.Dispose();
        }
    }
}
