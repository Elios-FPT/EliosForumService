using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects.Comment
{
    /// <summary>
    /// Represents a comment, including author details and nested replies.
    /// </summary>
    public class CommentDto
    {
        public Guid CommentId { get; set; }
        public Guid AuthorId { get; set; }
        public Guid? ParentCommentId { get; set; } 
        public string Content { get; set; } = string.Empty;
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public DateTime CreatedAt { get; set; }

        public string? AuthorFirstName { get; set; }
        public string? AuthorLastName { get; set; }
        public string? AuthorAvatarUrl { get; set; }
        public string AuthorFullName => $"{AuthorFirstName} {AuthorLastName}".Trim();

        public List<CommentDto> Replies { get; set; } = new();
    }
}
