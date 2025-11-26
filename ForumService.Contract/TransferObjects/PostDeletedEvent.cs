using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects
{
    public class PostDeletedEvent
    {
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public DateTime DeletedAt { get; set; }
    }
}
