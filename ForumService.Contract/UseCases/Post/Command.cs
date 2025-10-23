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
        /// Command to create a new post, including file uploads.
        /// </summary>
        public record CreatePostCommand(
            Guid AuthorId,
            Guid? CategoryId,
            string Title,
            string? Summary,
            string Content,
            string? PostType,
            // CHANGE HERE:
            // Replace the old Attachment list with a list containing raw file data.
            List<FileToUploadDto>? FilesToUpload
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

            // CHANGE: Raw file data for NEW files to be uploaded.
            List<FileToUploadDto>? NewFilesToUpload,

            // CHANGE: List of IDs of OLD attachments to be deleted.
            List<Guid>? AttachmentIdsToDelete
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
            List<string> Tags // Added tag list here
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
    }
}
