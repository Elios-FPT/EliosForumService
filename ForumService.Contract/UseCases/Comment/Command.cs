using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Comment
{
    public static class Command
    {
        /// <summary>
        /// Command to create a new comment. Includes the author's ID.
        /// </summary>
        public record CreateCommentCommand(
            Guid PostId,
            Guid? ParentCommentId,
            Guid AuthorId, // ID of the user creating the comment
            string Content
        ) : ICommand<BaseResponseDto<Guid>>; // Return the ID of the newly created comment

        /// <summary>
        /// Command to update an existing comment.
        /// </summary>
        public record UpdateCommentCommand(
            Guid CommentId,
            Guid RequesterId,
            string Content
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to delete a comment.
        /// </summary>
        public record DeleteCommentCommand(
            Guid CommentId,
            Guid RequesterId
        ) : ICommand<BaseResponseDto<bool>>;
    }
}
