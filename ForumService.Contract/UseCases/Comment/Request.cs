using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Comment
{
    public static class Request
    {
        /// <summary>
        /// Request to create a new comment or reply.
        /// </summary>
        public record CreateCommentRequest(
            [Required] Guid PostId, // ID of the post being commented on
            Guid? ParentCommentId, // Optional: ID of the comment being replied to
            [Required, MinLength(1)] string Content // The content of the comment
        );

        /// <summary>
        /// Request to update an existing comment's content.
        /// </summary>
        public record UpdateCommentRequest(
            [Required, MinLength(1)] string Content // The new content of the comment
        );
    }
}
