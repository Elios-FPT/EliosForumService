using Confluent.Kafka;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class KafkaConsumerRepository<T> : IKafkaConsumerRepository<T>, IDisposable where T : class
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConsumer<string, string> _consumer;
        private readonly string _commandTopic;
        private readonly string _responseTopic;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ConcurrentDictionary<string, bool> _processedCorrelationIds;
        private readonly IKafkaResponseHandler<T> _responseHandler;
        private readonly IConfiguration _configuration;
        private readonly string _destinationServiceName;
        private readonly CancellationTokenSource _cts;

        private const string CREATE_EVENT = "CREATE";
        private const string UPDATE_EVENT = "UPDATE";
        private const string DELETE_EVENT = "DELETE";
        private const string GET_ALL_EVENT = "GET_ALL";
        private const string GET_BY_ID_EVENT = "GET_BY_ID";

        public KafkaConsumerRepository(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IAppConfiguration appConfiguration,
            IKafkaResponseHandler<T> responseHandler,
            string sourceServiceName)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _ = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _responseHandler = responseHandler ?? throw new ArgumentNullException(nameof(responseHandler));
            _ = sourceServiceName ?? throw new ArgumentNullException(nameof(sourceServiceName));

            _destinationServiceName = _configuration["Kafka:CurrentService"]
                ?? throw new InvalidOperationException("Kafka:CurrentService configuration is missing.");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultBufferSize = 4096
            };
            _processedCorrelationIds = new ConcurrentDictionary<string, bool>();
            _cts = new CancellationTokenSource();

            var bootstrapServers = _configuration["Kafka:BootstrapServers"]
                ?? throw new InvalidOperationException("Kafka:BootstrapServers configuration is missing.");

            _commandTopic = $"{sourceServiceName}-{_destinationServiceName}-{typeof(T).Name.ToLower()}";
            _responseTopic = $"{_destinationServiceName}-{sourceServiceName}-{typeof(T).Name.ToLower()}";

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = $"{_destinationServiceName}-consumer-group-{sourceServiceName}-{typeof(T).Name.ToLower()}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
                EnablePartitionEof = true,
                GroupInstanceId = $"consumer-instance-{Guid.NewGuid()}",
                SessionTimeoutMs = 30000,
                MaxPollIntervalMs = 300000,
                FetchMaxBytes = 1024 * 1024,
                MaxPartitionFetchBytes = 512 * 1024
            };
            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            _consumer.Subscribe(_commandTopic);

            Console.WriteLine($"[{_destinationServiceName}] Subscribed to topic: {_commandTopic} from {sourceServiceName}-service");
        }

        public async Task StartConsumingAsync(CancellationToken cancellationToken = default)
        {
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ConsumeLoopAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Kafka consumer cancelled for {_commandTopic}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Background consumer ERROR {_commandTopic}: {ex.Message}");
                }
            }, linkedCts.Token);

            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Kafka consumer STARTED (background) for {_commandTopic}");
        }

        private async Task ConsumeLoopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Background Kafka loop STARTED for {_commandTopic}");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));

                    if (consumeResult == null)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    if (consumeResult.Message == null)
                    {
                        if (consumeResult.IsPartitionEOF)
                        {
                            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Reached EOF on partition {consumeResult.Partition} for topic {_commandTopic}");
                        }
                        continue;
                    }

                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Received message from {_commandTopic}: {consumeResult.Message.Key}");
                    await ProcessMessageAsync(consumeResult, cancellationToken);

                    _consumer.Commit(consumeResult);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Consume error {_commandTopic}: {ex.Error.Reason}");
                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Consumer ERROR {_commandTopic}: {ex.Message}");
                    await _responseHandler.SendToDeadLetterQueueAsync(ex.Message, ex);
                    await Task.Delay(2000, cancellationToken);
                }
            }

            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Background Kafka loop STOPPED for {_commandTopic}");
        }

        public async Task StopConsumingAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Stopping Kafka consumer for {_commandTopic}");
            _cts.Cancel();
            await Task.Delay(1000, cancellationToken);
        }

        private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(consumeResult.Message.Value))
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Skipping empty message from {_commandTopic}");
                    return;
                }

                EventWrapper? wrapper = null;
                try
                {
                    wrapper = JsonSerializer.Deserialize<EventWrapper>(consumeResult.Message.Value, _jsonOptions);
                }
                catch (JsonException jsonEx)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] JSON Parse error {_commandTopic}: {jsonEx.Message}");
                    Console.WriteLine($"Raw message: {consumeResult.Message.Value?.Substring(0, Math.Min(100, consumeResult.Message.Value.Length))}...");
                    return;
                }

                if (wrapper == null)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Skipping null wrapper from {_commandTopic}");
                    return;
                }

                if (wrapper.ModelType != typeof(T).Name)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Skipping wrong ModelType: {wrapper.ModelType} (expected: {typeof(T).Name})");
                    return;
                }

                if (string.IsNullOrEmpty(wrapper.CorrelationId))
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Skipping message without CorrelationId from {_commandTopic}");
                    return;
                }

                if (!_processedCorrelationIds.TryAdd(wrapper.CorrelationId, true))
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Skipping duplicate CorrelationId: {wrapper.CorrelationId}");
                    return;
                }

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Processing {wrapper.EventType} | Corr: {wrapper.CorrelationId} | Topic: {_commandTopic}");

                await ProcessEventAsync(wrapper, cancellationToken);

                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Completed {wrapper.EventType} | Corr: {wrapper.CorrelationId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] ProcessMessage ERROR {_commandTopic}: {ex.Message}");
                await _responseHandler.SendToDeadLetterQueueAsync($"Process error: {ex.Message}", ex);
            }
            finally
            {
                if (!string.IsNullOrEmpty(consumeResult.Message?.Key))
                    _processedCorrelationIds.TryRemove(consumeResult.Message.Key, out _);
            }
        }

        private async Task ProcessEventAsync(EventWrapper wrapper, CancellationToken cancellationToken)
        {
            switch (wrapper.EventType)
            {
                case CREATE_EVENT:
                    if (wrapper.Payload != null)
                        await HandleCreateAsync(wrapper, cancellationToken);
                    break;
                case UPDATE_EVENT:
                    if (wrapper.Payload != null)
                        await HandleUpdateAsync(wrapper, cancellationToken);
                    break;
                case DELETE_EVENT:
                    await HandleDeleteAsync(wrapper, cancellationToken);
                    break;
                case GET_ALL_EVENT:
                    await HandleGetAllAsync(wrapper, cancellationToken);
                    break;
                case GET_BY_ID_EVENT:
                    await HandleGetByIdAsync(wrapper, cancellationToken);
                    break;
                default:
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Unknown EventType: {wrapper.EventType}");
                    break;
            }
        }

        private async Task HandleGetAllAsync(EventWrapper wrapper, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<T>>();
            var results = await repository.GetListAsync();
            await _responseHandler.SendGetAllResponseAsync(wrapper.CorrelationId, results, _responseTopic);
        }

        private async Task HandleGetByIdAsync(EventWrapper wrapper, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<T>>();
            var id = Guid.Parse(wrapper.Payload.ToString());
            var result = await repository.GetByIdAsync(id);
            await _responseHandler.SendGetByIdResponseAsync(wrapper.CorrelationId, result, _responseTopic);
        }

        private async Task HandleCreateAsync(EventWrapper wrapper, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<T>>();

            try
            {
                var entity = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(wrapper.Payload), _jsonOptions);
                if (entity is null) return;

                await repository.AddAsync(entity);
                await repository.SaveChangesAsync();
                await _responseHandler.SendCreatedResponseAsync(entity, wrapper.CorrelationId, _responseTopic);
            }
            catch (Exception ex)
            {
                await _responseHandler.SendErrorResponseAsync(wrapper, $"Error creating entity: {ex.Message}", _responseTopic);
            }
        }

        private async Task HandleUpdateAsync(EventWrapper wrapper, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<T>>();

            try
            {
                var entity = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(wrapper.Payload), _jsonOptions);
                if (entity is null) return;

                var idProperty = typeof(T).GetProperty("Id") ?? throw new InvalidOperationException("Entity must have an Id property.");
                var entityId = (Guid)idProperty.GetValue(entity);

                var existingEntity = await repository.GetByIdAsync(entityId);
                if (existingEntity is null)
                {
                    await _responseHandler.SendErrorResponseAsync(wrapper, $"Entity with ID {entityId} not found.", _responseTopic);
                    return;
                }

                foreach (var prop in typeof(T).GetProperties())
                {
                    if (prop.Name != "Id" && prop.CanWrite)
                        prop.SetValue(existingEntity, prop.GetValue(entity));
                }

                await repository.UpdateAsync(existingEntity);
                await repository.SaveChangesAsync();
                await _responseHandler.SendUpdatedResponseAsync(existingEntity, wrapper.CorrelationId, _responseTopic);
            }
            catch (Exception ex)
            {
                await _responseHandler.SendErrorResponseAsync(wrapper, $"Error updating entity: {ex.Message}", _responseTopic);
            }
        }

        private async Task HandleDeleteAsync(EventWrapper wrapper, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<T>>();

            try
            {
                var id = Guid.Parse(wrapper.Payload.ToString());
                var existingEntity = await repository.GetByIdAsync(id);
                if (existingEntity is null)
                {
                    await _responseHandler.SendErrorResponseAsync(wrapper, $"Entity with ID {id} not found.", _responseTopic);
                    return;
                }

                await repository.DeleteAsync(existingEntity);
                await repository.SaveChangesAsync();
                await _responseHandler.SendDeletedResponseAsync(id, wrapper.CorrelationId, _responseTopic);
            }
            catch (Exception ex)
            {
                await _responseHandler.SendErrorResponseAsync(wrapper, $"Error deleting entity: {ex.Message}", _responseTopic);
            }
        }

        public void Dispose()
        {
            try
            {
                _cts?.Cancel();
                _consumer?.Close();
                _consumer?.Dispose();
                _processedCorrelationIds.Clear();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during disposal: {ex.Message}");
            }
        }
    }
}