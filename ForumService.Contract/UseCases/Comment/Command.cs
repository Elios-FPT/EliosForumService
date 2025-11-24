using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
            [property: Required(ErrorMessage = "PostId is required")]
            Guid PostId,

            Guid? ParentCommentId,

            [property: Required(ErrorMessage = "AuthorId is required")]
            Guid AuthorId, // ID of the user creating the comment

            [property: Required(ErrorMessage = "Content is required")]
            [property: StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 2000 characters")]
            string Content
        ) : ICommand<BaseResponseDto<Guid>>; 

        /// <summary>
        /// Command to update an existing comment.
        /// </summary>
        public record UpdateCommentCommand(
            [property: Required(ErrorMessage = "CommentId is required")]
            Guid CommentId,

            [property: Required(ErrorMessage = "RequesterId is required")]
            Guid RequesterId,

            [property: Required(ErrorMessage = "Content is required")]
            [property: StringLength(2000, MinimumLength = 1, ErrorMessage = "Content must be between 1 and 2000 characters")]
            string Content
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to delete a comment.
        /// </summary>
        public record DeleteCommentCommand(
            [property: Required(ErrorMessage = "CommentId is required")]
            Guid CommentId,

            [property: Required(ErrorMessage = "RequesterId is required")]
            Guid RequesterId
        ) : ICommand<BaseResponseDto<bool>>;
    }
}