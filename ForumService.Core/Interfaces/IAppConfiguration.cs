using ForumService.Core.Models;

namespace ForumService.Core.Interfaces
{
    public interface IAppConfiguration
    {
 
        string GetKafkaBootstrapServers();
        string GetCurrentServiceName();
    }
}
