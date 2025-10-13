using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ForumService.Core.Interfaces;

namespace ForumService.Infrastructure.Kafka
{
    public class KafkaConsumerHostedService<T> : BackgroundService where T : class
    {
        private readonly IServiceProvider _provider;

        public KafkaConsumerHostedService(IServiceProvider provider)
        {
            _provider = provider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var scope = _provider.CreateScope();
            var kafkaRepo = scope.ServiceProvider.GetRequiredService<IKafkaRepository<T>>();

            Console.WriteLine($"[KafkaConsumer] Listening for {typeof(T).Name} events...");
            await kafkaRepo.StartConsumingAsync(stoppingToken);
        }
    }

}
