using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    public class Comment
    {
        [Key]
        public Guid CommentId { get; set; }
        public Guid PostId { get; set; }
        public Guid? ParentCommentId { get; set; }
        public Guid AuthorId { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public string Content { get; set; } = string.Empty;
        public int UpvoteCount { get; set; } = 0; // Tracks total upvotes
        public int DownvoteCount { get; set; } = 0; // Tracks total downvotes
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }

        // Navigation Properties
        public virtual Post Post { get; set; } = null!;
        public virtual Comment? ParentComment { get; set; }
        public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();
    }
}
