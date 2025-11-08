using Asp.Versioning;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static ForumService.Contract.UseCases.Post.Command;
using static ForumService.Contract.UseCases.Post.Query;
using static ForumService.Contract.UseCases.Post.Request;

namespace ForumService.Web.Controllers.Post
{
    /// <summary>
    /// Endpoints for content moderators to manage posts.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("ModeratorPost")]
    [Route("api/forum/moderator/posts")]
    public class ModeratorPostController : ControllerBase
    {
        protected readonly ISender _sender;

        public ModeratorPostController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Retrieves a paginated list of PUBLISHED posts for moderator view (includes moderation details).
        /// </summary>
        [HttpGet("published")] // New route specific for published posts in moderator view
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<ModeratorPostViewDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<IEnumerable<ModeratorPostViewDto>>> GetModeratorPublicPosts([FromQuery] GetModeratorPublicPostsRequest request)
        {
            // Map request to query
            var query = new GetModeratorPublicPostsQuery(
                AuthorId: request.AuthorId,
                CategoryId: request.CategoryId,
                PostType: request.PostType,
                SearchKeyword: request.SearchKeyword,
                Limit: request.Limit,
                Offset: request.Offset,
                SortBy: request.SortBy,
                SortOrder: request.SortOrder
            );
            return await _sender.Send(query);
            
        }

        /// <summary>
        /// Retrieves a list of posts pending review.
        /// </summary>
        [HttpGet("pending")]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<ModeratorPostViewDto>>), StatusCodes.Status200OK)]
        public async Task<BaseResponseDto<IEnumerable<ModeratorPostViewDto>>> GetPendingPosts([FromQuery] GetPendingPostsQuery request)
        {
            var query = new GetPendingPostsQuery(
               AuthorId: request.AuthorId,
                CategoryId: request.CategoryId,
                PostType: request.PostType,
                SearchKeyword: request.SearchKeyword,
                Limit: request.Limit,
                Offset: request.Offset,
                SortBy: request.SortBy,
                SortOrder: request.SortOrder
            );
            return await _sender.Send(query);
        }

        /// <summary>
        /// Retrieves a list of archived (rejected or soft-deleted) posts.
        /// </summary>
        [HttpGet("archived")]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<ModeratorPostViewDto>>), StatusCodes.Status200OK)]
        public async Task<BaseResponseDto<IEnumerable<ModeratorPostViewDto>>> GetArchivedPosts([FromQuery] GetArchivedPostsQuery request)
        {
            var query = new GetArchivedPostsQuery(
                AuthorId: request.AuthorId,
                CategoryId: request.CategoryId,
                PostType: request.PostType,
                SearchKeyword: request.SearchKeyword,
                Limit: request.Limit,
                Offset: request.Offset,
                SortBy: request.SortBy,
                SortOrder: request.SortOrder
            );
            return await _sender.Send(query);
        }


        /// <summary>
        /// Approves a post pending review.
        /// </summary>
        /// <remarks>
        /// Changes the post status from "PendingReview" to "Published".
        /// </remarks>
        /// <param name="postId">The ID of the post to approve.</param>
        /// <returns>A boolean indicating success.</returns>
        [HttpPut("{postId}/approve")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> ApprovePost([FromRoute] Guid postId)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var moderatorId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }
            //var moderatorId = new Guid("ac9f879f-5121-45ab-bd47-641e68934105");

            var command = new ApprovePostCommand(postId, moderatorId);
            return await _sender.Send(command);
        }

        /// <summary>
        /// Rejects a post pending review.
        /// </summary>
        /// <remarks>
        /// Changes the post status from "PendingReview" to "Rejected".
        /// </remarks>
        /// <param name="postId">The ID of the post to reject.</param>
        /// <param name="request">An object containing the reason for rejection.</param>
        /// <returns>A boolean indicating success.</returns>
        [HttpPut("{postId}/reject")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> RejectPost([FromRoute] Guid postId, [FromBody] RejectPostRequest request)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var moderatorId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }

            //var moderatorId = new Guid("ac9f879f-5121-45ab-bd47-641e68934105");

            var command = new RejectPostCommand(postId, moderatorId, request.Reason);
            return await _sender.Send(command);
        }

        /// <summary>
        /// (Moderator) Soft-deletes a post.
        /// </summary>
        /// <remarks>
        /// Sets the IsDeleted flag to true. Can delete posts in any status.
        /// Requires a reason in the request body.
        /// </remarks>
        /// <param name="postId">The ID of the post to delete.</param>
        /// <param name="request">An object containing the reason for deletion.</param>
        /// <returns>A boolean indicating success.</returns>
        [HttpDelete("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> ModeratorDeletePost([FromRoute] Guid postId, [FromBody] ModeratorDeletePostRequest request)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var moderatorId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }

            //var moderatorId = new Guid("ac9f879f-5121-45ab-bd47-641e68934105");

            var command = new ModeratorDeletePostCommand(postId, moderatorId, request.Reason);
            return await _sender.Send(command);
        }
    }
}
