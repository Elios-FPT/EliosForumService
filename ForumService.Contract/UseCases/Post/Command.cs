using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Post
{
    public static class Command
    {
        /// <summary>
        /// Unified Command to create a new post.
        /// </summary>
        public record CreatePostCommand(
            [property: Required] Guid AuthorId,
            Guid? CategoryId, 

            [property: Required(ErrorMessage = "Title is required")]
            [property: StringLength(255, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 255 characters")]
            string Title,

            [property: Required(ErrorMessage = "Content is required")]
            string Content,

            [property: RegularExpression("^(Post|Solution|Project)$", ErrorMessage = "Invalid PostType")]
            string? PostType,

            Guid? ReferenceId,
            [property: MaxLength(10, ErrorMessage = "Cannot have more than 10 tags")]
            List<string>? Tags,

            bool SubmitForReview
        ):ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to update an existing post, including file attachment handling.
        /// </summary>
        public record UpdatePostCommand(
            [property: Required(ErrorMessage = "RequesterId is required")]
            Guid RequesterId,

            [property: Required(ErrorMessage = "PostId is required")]
            Guid PostId,

            [property: Required(ErrorMessage = "Title is required")]
            [property: StringLength(255, MinimumLength = 5, ErrorMessage = "Title must be between 5 and 255 characters")]
            string Title,

            [property: Required(ErrorMessage = "Content is required")]
            string Content,

            Guid? CategoryId,

            [property: MaxLength(10, ErrorMessage = "You can only add up to 10 tags")]
            List<string>? Tags,

            Guid? ReferenceId,

            bool SubmitForReview
        ):ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to delete a post.
        /// </summary>
        public record DeletePostCommand(

            [property: Required(ErrorMessage = "PostId is required")]
            Guid PostId,

            [property: Required(ErrorMessage = "RequesterId is required")]
            Guid RequesterId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to toggle the featured status of a post.
        /// </summary>
        public record ToggleFeaturedCommand(
            Guid PostId,
            bool IsFeatured
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to increment the view count of a post.
        /// </summary>
        public record IncrementViewCountCommand(
            Guid PostId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to submit a post for review, including its associated tags.
        /// </summary>
        public record SubmitPostForReviewCommand(
            Guid PostId,
            Guid RequesterId,
            List<string>? Tags
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to like or unlike a post.
        /// </summary>
        public record ToggleLikePostCommand(
            Guid PostId,
            Guid UserId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command model for creating an attachment.
        /// </summary>
        public record CreateAttachmentCommand(
            string Filename,
            string Url,
            string? ContentType,
            long? SizeBytes
        );

        /// <summary>
        /// Command for a moderator to approve a post.
        /// </summary>
        public record ApprovePostCommand(
            Guid PostId,
            Guid ModeratorId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command for a moderator to reject a post, with an optional reason.
        /// </summary>
        public record RejectPostCommand(
            Guid PostId,
            Guid ModeratorId,
            string? Reason
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to upvote a post.
        /// </summary>
        public record UpvotePostCommand(
            Guid PostId,
            Guid RequesterId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to downvote a post.
        /// </summary>
        public record DownvotePostCommand(
            Guid PostId,
            Guid RequesterId
        ) : ICommand<BaseResponseDto<bool>>;

        
        public record ModeratorDeletePostCommand(
            Guid PostId,
            Guid ModeratorId, 
            string Reason     
        ) : ICommand<BaseResponseDto<bool>>;

    }
}
