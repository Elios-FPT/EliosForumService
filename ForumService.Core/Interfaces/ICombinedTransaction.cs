using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces
{
    public interface ICombinedTransaction : IDisposable
    {
        IKafkaTransaction KafkaTransaction { get; }
        IUnitOfWork DbTransaction { get; }
    }
}
