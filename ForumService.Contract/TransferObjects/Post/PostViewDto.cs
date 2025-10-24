using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects.Post
{
    public class PostViewDto
    {
        public Guid PostId { get; set; }
        public Guid AuthorId { get; set; } = Guid.Empty;
        public Guid? CategoryId { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; }
        public string PostType { get; set; }
        public string Status { get; set; }
        public long ViewsCount { get; set; }
        public int CommentCount { get; set; }
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? CategoryName { get; set; }
        public List<string>? Tags { get; set; } = new();

        public string? AuthorFirstName { get; set; }
        public string? AuthorLastName { get; set; }
        public string? AuthorAvatarUrl { get; set; }
        public string AuthorFullName => $"{AuthorFirstName} {AuthorLastName}".Trim();
    }
}
