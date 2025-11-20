using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    public class BannedKeyword
    {
        public Guid Id { get; set; }
        public string Keyword { get; set; } 
        public bool IsActive { get; set; } 
        public DateTime CreatedAt { get; set; }
    }
}
