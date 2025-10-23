using ForumService.Contract.TransferObjects.Comment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects.Post
{
    public class PostViewDetailDto
    {
        public Guid PostId { get; set; }
        public Guid AuthorId { get; set; }
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public string Content { get; set; } = null!;
        public string PostType { get; set; }
        public long ViewsCount { get; set; }
        public int CommentCount { get; set; }
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? CategoryName { get; set; }
        public List<string>? Tags { get; set; } = new();
        public List<string>? Url { get; set; } = new();

        public List<CommentDto> Comments { get; set; } = new();

        public string? AuthorFirstName { get; set; }
        public string? AuthorLastName { get; set; }
        public string? AuthorAvatarUrl { get; set; }
        public string AuthorFullName => $"{AuthorFirstName} {AuthorLastName}".Trim();
    }
}
