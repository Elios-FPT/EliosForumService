using Microsoft.Extensions.Configuration;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations
{
    public class AppConfiguration : IAppConfiguration
    {
        private readonly IConfiguration _config;

        public AppConfiguration(IConfiguration config)
        {
            _config = config;
        }

        public EmailConfiguration GetEmailConfiguration()
            => _config.GetSection("EmailConfiguration").Get<EmailConfiguration>()
               ?? throw new InvalidOperationException("Missing EmailConfiguration section.");

        public string GetKafkaBootstrapServers()
            => _config.GetValue<string>("Kafka:BootstrapServers")
               ?? throw new InvalidOperationException("Missing Kafka BootstrapServers configuration.");
    }
}
