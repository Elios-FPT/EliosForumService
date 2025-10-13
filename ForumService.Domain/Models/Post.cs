using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ForumService.Domain.Models
{
    public class Post
    {
        [Key]
        public Guid PostId { get; set; }
        public Guid AuthorId { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public Guid? CategoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public string Content { get; set; } = null!;
        public string PostType { get; set; } = "Post"; // "Post" | "Solution"
        public string Status { get; set; } = "Draft"; // "Draft" | "PendingReview" | "Rejected" | "Published" |
        public long ViewsCount { get; set; } = 0;
        public int CommentCount { get; set; } = 0;
        public int UpvoteCount { get; set; } = 0; // Tracks total upvotes
        public int DownvoteCount { get; set; } = 0; // Tracks total downvotes
        public bool IsFeatured { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid CreatedBy { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public DateTime? DeletedAt { get; set; }
        public Guid? DeletedBy { get; set; } // Reference to user ID, fetched from UserService Redis cache

        // Navigation Properties
        public virtual Category? Category { get; set; }
        public virtual ICollection<PostTag> PostTags { get; set; } = new List<PostTag>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
