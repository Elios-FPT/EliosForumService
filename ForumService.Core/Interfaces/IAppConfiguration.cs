using ForumService.Core.Models;

namespace ForumService.Core.Interfaces
{
    public interface IAppConfiguration
    {
        EmailConfiguration GetEmailConfiguration();
        string GetKafkaBootstrapServers();
    }
}
