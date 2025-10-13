using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    public interface IKafkaTransaction : IDisposable
    {
        IKafkaProducer Producer { get; }
    }
}
