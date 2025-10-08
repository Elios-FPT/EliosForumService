using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    public class Tag
    {
        [Key]
        public Guid TagId { get; set; }
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PostTag>? PostTags { get; set; }
    }
}
