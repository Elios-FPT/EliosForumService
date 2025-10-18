using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Contract.UseCases.Post
{
    public static class Request
    {
        /// <summary>
        /// Request to create a new post.
        /// </summary>
        public record CreatePostRequest(
            Guid AuthorId,
            Guid? CategoryId,
            [Required, MaxLength(255)] string Title,
            string? Summary,
            [Required] string Content,
            string PostType = "Post"   // "Post" | "Solution"
        );

        /// <summary>
        /// Request to update an existing post.
        /// </summary>
        public record UpdatePostRequest(
            Guid PostId,
            [Required, MaxLength(255)] string Title,
            string? Summary,
            [Required] string Content,
            Guid? CategoryId,
            string Status,
            List<CreateAttachmentRequest>? Attachments = null
        );

        /// <summary>
        /// Request to delete a specific post by ID.
        /// </summary>
        public record DeletePostRequest(
            Guid PostId
        );

        /// <summary>
        /// Request to get a single post by ID.
        /// </summary>
        public record GetPostByIdRequest(
            Guid PostId
        );

        /// <summary>
        /// Request to get paginated posts with optional filters.
        /// </summary>
        public record GetPublicViewPostsRequest(
             // Các trường lọc cũ
             Guid? AuthorId = null,
             Guid? CategoryId = null,
             string? PostType = null,
             string? SearchKeyword = null,

             // Phân trang
             int Limit = 20,
             int Offset = 0,

             // Các trường mới thêm vào
             List<string>? Tags = null,      // ✨ Lọc theo danh sách tags
             string? SortBy = null,          // ✨ Sắp xếp theo trường nào (vd: "ViewsCount")
             string? SortOrder = null        // ✨ Thứ tự sắp xếp (vd: "ASC" hoặc "DESC")
         );

        /// <summary>
        /// Request to increment view count for a post.
        /// </summary>
        public record IncrementViewCountRequest(
            Guid PostId
        );

        /// <summary>
        /// Request to toggle featured status of a post.
        /// </summary>
        public record ToggleFeaturedRequest(
            Guid PostId,
            bool IsFeatured
        );

        /// <summary>
        /// Request to get total post count by author.
        /// </summary>
        public record GetPostCountByAuthorRequest(
            Guid AuthorId
        );

        /// <summary>
        /// Request model for creating an attachment.
        /// </summary>
        public record CreateAttachmentRequest(
            string Filename,
            string Url,
            string? ContentType,
            long? SizeBytes
        );

    }
}
