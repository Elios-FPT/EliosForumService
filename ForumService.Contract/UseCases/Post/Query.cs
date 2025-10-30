using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Post
{
    public static class Query
    {
        // --- Queries for public users (Public API) ---

        /// <summary>
        /// Query to get a paginated list of PUBLISHED posts for public view.
        /// This is the most complex query, offering full filtering, sorting, and pagination.
        /// </summary>
        public record GetPublicViewPostsQuery(
            Guid? AuthorId = null,
            Guid? CategoryId = null,
            string? PostType = null,
            string? SearchKeyword = null,
            int Limit = 20,
            int Offset = 0,
            string? SortBy = null,
            string? SortOrder = null
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;


        // --- Queries for moderators (Moderator API) ---

        /// <summary>
        /// Query specifically for moderators to get a paginated list of 'Published' posts.
        /// Returns the richer ModeratorPostViewDto.
        /// </summary>
        public record GetModeratorPublicPostsQuery(
             Guid? AuthorId = null,
             Guid? CategoryId = null,
             string? PostType = null,
             string? SearchKeyword = null,
             int Limit = 20,
             int Offset = 0,
             string? SortBy = null, 
             string? SortOrder = null
        ) : IQuery<BaseResponseDto<IEnumerable<ModeratorPostViewDto>>>;


        /// <summary>
        /// Query for moderators to get a paginated list of posts with 'PendingReview' status.
        /// This query is simpler, focusing on basic pagination.
        /// </summary>
        public record GetPendingPostsQuery(
             Guid? AuthorId = null,
             Guid? CategoryId = null,
             string? PostType = null,
             string? SearchKeyword = null,
             int Limit = 20,
             int Offset = 0,
             string? SortBy = null,
             string? SortOrder = null
        ) : IQuery<BaseResponseDto<IEnumerable<ModeratorPostViewDto>>>;

        /// <summary>
        /// Query for moderators to get a paginated list of 'Rejected' or soft-deleted posts.
        /// </summary>
        public record GetArchivedPostsQuery(
             Guid? AuthorId = null,
             Guid? CategoryId = null,
             string? PostType = null,
             string? SearchKeyword = null,
             int Limit = 20,
             int Offset = 0,
             string? SortBy = null,
             string? SortOrder = null
        ) : IQuery<BaseResponseDto<IEnumerable<ModeratorPostViewDto>>>;

        /// <summary>
        /// Query to get all posts belonging to a specific user (the requester).
        /// </summary>
        public record GetMyPostsQuery(
            Guid RequesterId, // <-- ID of the currently logged-in user
                              // Filtering
            string? Status = null,
            Guid? CategoryId = null,
            string? PostType = null,
            string? SearchKeyword = null,
            // Pagination & Sorting
            int Limit = 20,
            int Offset = 0,
            string? SortBy = null,
            string? SortOrder = null
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;

        /// <summary>
        /// Query to get the detailed view of a single published post.
        /// </summary>
        public record GetPostDetailsByIdQuery(
            Guid PostId
        ) : IQuery<BaseResponseDto<PostViewDetailDto>>;

        /// <summary>
        /// Query to get the total number of posts created by a specific author.
        /// </summary>
        public record GetPostCountByAuthorQuery(
            Guid AuthorId
        ) : IQuery<BaseResponseDto<int>>;

        /// <summary>
        /// Query to get a list of featured posts.
        /// </summary>
        public record GetFeaturedPostsQuery(
            int Limit = 10,
            int Offset = 0
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;

        /// <summary>
        /// Query to get a list of posts by category.
        /// </summary>
        public record GetPostsByCategoryQuery(
            Guid CategoryId,
            int Limit = 20,
            int Offset = 0
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;
    }
}
