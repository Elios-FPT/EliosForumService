using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    public class Vote
    {
        [Key]
        public Guid VoteId { get; set; }
        public Guid UserId { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public string TargetType { get; set; } = null!; // E.g., "Post" or "Comment"
        public Guid TargetId { get; set; } // ID of the target entity
        public string VoteType { get; set; } = null!; // "Upvote" or "Downvote"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
