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
        /// Command to create a new post.
        /// </summary>
        /// <summary>
        /// Command để tạo một bài viết mới, bao gồm cả các file cần upload.
        /// </summary>
        public record CreatePostCommand(
            Guid AuthorId,
            Guid? CategoryId,
            string Title,
            string? Summary,
            string Content,
            string? PostType,
            // THAY ĐỔI TẠI ĐÂY:
            // Thay thế list Attachment cũ bằng một list chứa dữ liệu file thô.
            List<FileToUploadDto>? FilesToUpload
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to update an existing post.
        /// </summary>
        public record UpdatePostCommand(
            Guid PostId,
            string Title,
            string? Summary,
            string Content,
            Guid? CategoryId,
            List<CreateAttachmentCommand>? Attachments,
            string Status
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to delete a post.
        /// </summary>
        public record DeletePostCommand(
            Guid PostId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to change post's featured status.
        /// </summary>
        public record ToggleFeaturedCommand(
            Guid PostId,
            bool IsFeatured
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to increment view count for a post.
        /// </summary>
        public record IncrementViewCountCommand(
            Guid PostId
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command to change post's status (e.g. Draft → Published).
        /// </summary>
        public record ChangePostStatusCommand(
            Guid PostId,
            string Status
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
    }
}
