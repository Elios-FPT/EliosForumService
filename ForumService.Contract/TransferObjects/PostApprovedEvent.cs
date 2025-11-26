using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects
{
    public class PostApprovedEvent
    {
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public DateTime ApprovedAt { get; set; }
    }
}
