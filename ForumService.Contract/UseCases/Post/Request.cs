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
        /// Unified Request to create a new post (Draft or PendingReview).
        /// </summary>
        public record CreatePostRequest(
            Guid? CategoryId,
            [Required, MaxLength(255)] string Title,
            [Required] string Content,
            string PostType = "Post",      // "Post" | "Solution"
            Guid? ReferenceId = null,
            List<string>? Tags = null,     // Optional: Tags defined at creation time
            bool SubmitForReview = false   // FALSE = Draft (default), TRUE = PendingReview
        );

        /// <summary>
        /// Request to update an existing post. (Uses JSON [FromBody])
        /// </summary>
        public record UpdatePostRequest(
            [Required, MaxLength(255)] string Title,
            string? Summary, 
            [Required] string Content,
            Guid? CategoryId,
            Guid? ReferenceId = null,
            List<string>? Tags = null
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
        /// Request to get a paginated list of published posts with optional filters for public view.
        /// </summary>
        public record GetPublishedPostsRequest(
            // Filtering
            Guid? AuthorId = null,
            Guid? CategoryId = null,
            string? PostType = null,
            string? SearchKeyword = null,

            // Pagination
            int Limit = 20,
            int Offset = 0,

            // Sorting
            string? SortBy = null,     // e.g., "ViewsCount", "CreatedAt"
            string? SortOrder = null   // e.g., "ASC", "DESC"
        );

        public record GetModeratorPublicPostsRequest(
            // Filtering
            Guid? AuthorId = null,
            Guid? CategoryId = null,
            string? PostType = null,
            string? SearchKeyword = null,

            // Pagination
            int Limit = 20,
            int Offset = 0,

            // Sorting
            string? SortBy = null,    // e.g., "ViewsCount", "CreatedAt"
            string? SortOrder = null  // e.g., "ASC", "DESC"
        );

        /// <summary>
        /// Request to get all posts for the currently authenticated user.
        /// </summary>
        public record GetMyPostsRequest(
            // Filtering
            string? Status = null, // Allows filtering by Draft, PendingReview, etc.
            Guid? CategoryId = null,
            string? PostType = null,
            string? SearchKeyword = null,

            // Pagination
            int Limit = 20,
            int Offset = 0,

            // Sorting
            string? SortBy = null,
            string? SortOrder = null
        );


        /// <summary>
        /// Request for moderators to get a paginated list of posts pending review.
        /// </summary>
        public record GetPendingPostsRequest(
            // Filtering
            Guid? AuthorId = null,
            Guid? CategoryId = null,
            string? PostType = null,
            string? SearchKeyword = null,

            // Pagination
            int Limit = 20,
            int Offset = 0,

            // Sorting
            string? SortBy = null,    // e.g., "ViewsCount", "CreatedAt"
            string? SortOrder = null  // e.g., "ASC", "DESC"
        );

        /// <summary>
        /// Request for moderators to get a paginated list of archived posts.
        /// </summary>
        public record GetArchivedPostsRequest(
            // Filtering
            Guid? AuthorId = null,
            Guid? CategoryId = null,
            string? PostType = null,
            string? SearchKeyword = null,

            // Pagination
            int Limit = 20,
            int Offset = 0,

            // Sorting
            string? SortBy = null,    // e.g., "ViewsCount", "CreatedAt"
            string? SortOrder = null  // e.g., "ASC", "DESC"
        );

        /// <summary>
        /// Request to increment the view count of a post.
        /// </summary>
        public record IncrementViewCountRequest(
            Guid PostId
        );

        /// <summary>
        /// Request to toggle the featured status of a post.
        /// </summary>
        public record ToggleFeaturedRequest(
            Guid PostId,
            bool IsFeatured
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

        /// <summary>
        /// Request to submit a post for review, including its tags.
        /// </summary>
        public record SubmitPostForReviewRequest(
            List<string>? Tags
        );

        /// <summary>
        /// Request to reject a post, with an optional reason.
        /// </summary>
        public record RejectPostRequest(
            string? Reason
        );

        public record ModeratorDeletePostRequest
    (
        [Required(ErrorMessage = "A reason for deletion is required.")]
        [MinLength(10, ErrorMessage = "Reason must be at least 10 characters long.")]
        string Reason
    );
    }
}
