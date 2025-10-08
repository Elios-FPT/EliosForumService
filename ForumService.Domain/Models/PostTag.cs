using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    public class PostTag
    {
        [Key, Column(Order = 0)]
        public Guid PostId { get; set; }

        [Key, Column(Order = 1)]
        public Guid TagId { get; set; }

        // Navigation Properties
        public virtual Post Post { get; set; } = null!;
        public virtual Tag Tag { get; set; } = null!;
    }
}
