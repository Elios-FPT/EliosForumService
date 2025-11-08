using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using System;
using System.Collections.Generic;
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
            Guid AuthorId,
            Guid? CategoryId,
            string Title,
            string Content,
            string? PostType,
            Guid? ReferenceId,
            List<string>? Tags,
            bool SubmitForReview // True = PendingReview, False = Draft
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to update an existing post, including file attachment handling.
        /// </summary>
        public record UpdatePostCommand(
            Guid RequesterId,
            Guid PostId,
            string Title,
            string? Summary,
            string Content,
            Guid? CategoryId,
            List<string>? Tags,
            Guid? ReferenceId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to delete a post.
        /// </summary>
        public record DeletePostCommand(
            Guid PostId,
            // ADDED: The ID of the user requesting the deletion.
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
