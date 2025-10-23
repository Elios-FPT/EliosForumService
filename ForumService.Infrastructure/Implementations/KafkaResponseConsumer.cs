using Confluent.Kafka;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public static class KafkaResponseConsumer
    {
        private static readonly ConcurrentDictionary<string, IConsumer<string, string>> _consumers = new();
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TaskCompletionSource<object>>> _waiters = new();
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        private static string bootstrapServers;

        public static void Initialize(IAppConfiguration appConfiguration)
        {
            bootstrapServers = appConfiguration.GetKafkaBootstrapServers();
        }

        private static IConsumer<string, string> GetConsumer(string responseTopic)
        {
            if (!_consumers.TryGetValue(responseTopic, out var consumer))
            {
                var config = new ConsumerConfig
                {
                    BootstrapServers = bootstrapServers, 
                    GroupId = $"utility-response-group",
                    AutoOffsetReset = AutoOffsetReset.Latest,
                    EnableAutoCommit = false,
                    EnablePartitionEof = true
                };

                consumer = new ConsumerBuilder<string, string>(config).Build();
                consumer.Subscribe(responseTopic);

                _consumers[responseTopic] = consumer;
                _waiters[responseTopic] = new ConcurrentDictionary<string, TaskCompletionSource<object>>();

                Task.Run(() => ConsumeResponsesAsync(consumer, responseTopic));
            }

            return consumer;
        }

        private static async Task ConsumeResponsesAsync(IConsumer<string, string> consumer, string responseTopic)
        {
            while (consumer is not null)
            {
                try
                {
                    var result = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (result?.Message is null) continue;

                    var wrapper = JsonSerializer.Deserialize<EventWrapper>(result.Message.Value, _jsonOptions);
                    if (wrapper?.CorrelationId is null) continue;

                    if (_waiters.TryGetValue(responseTopic, out var waiters) &&
                        waiters.TryRemove(wrapper.CorrelationId, out var tcs))
                    {
                        if (wrapper.EventType == "ERROR_RESPONSE")
                            tcs.SetException(new Exception(wrapper.Payload?.ToString() ?? "Unknown error"));
                        else
                            tcs.SetResult(wrapper.Payload);

                        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] [RESPONSE] Completed: {wrapper.EventType} | CorrelationId: {wrapper.CorrelationId}");
                    }

                    consumer.Commit(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] Response consumer error for {responseTopic}: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }

        public static async Task<T> WaitForResponseAsync<T>(string correlationId, string responseTopic, CancellationToken cancellationToken = default)
        {
            var consumer = GetConsumer(responseTopic);
            var waiters = _waiters[responseTopic];

            var tcs = new TaskCompletionSource<object>();
            waiters[correlationId] = tcs;

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
            waiters.TryRemove(correlationId, out _);

            if (completed == tcs.Task)
            {
                var result = await tcs.Task;
                if (result is null) return default;

                if (typeof(T) == typeof(string))
                    return (T)(object)result.ToString();

                var temp = result.ToString();

                return JsonSerializer.Deserialize<T>(result.ToString(), _jsonOptions) ?? default;
            }

            throw new TimeoutException($"Response timeout for {correlationId} on topic {responseTopic}");
        }
    }
}