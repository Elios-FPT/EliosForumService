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
    /// Post management endpoints.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Post")]
    [Route("api/v1/[controller]")]
    public class PostController : ControllerBase
    {
        protected readonly ISender Sender;

        public PostController(ISender sender)
        {
            Sender = sender;
        }

        /// <summary>
        /// Creates a new post.
        /// </summary>
        /// <remarks>
        /// <pre>
        /// Description:
        /// This endpoint allows authenticated users with the `post:write` permission to create a new post.
        /// </pre>
        /// </remarks>
        /// <param name="request">A <see cref="CreatePostRequest"/> object containing the properties for the new post.</param>
        /// <returns>
        /// → <seealso cref="CreatePostCommand" /><br/>
        /// → <seealso cref="CreatePostCommandHandler" /><br/>
        /// → A <see cref="Result{TValue}"/> containing a boolean indicating success.<br/>
        /// </returns>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<BaseResponseDto<bool>> CreatePost([FromBody] CreatePostRequest request)
        {
            var command = new CreatePostCommand(
                AuthorId: request.AuthorId,
                CategoryId: request.CategoryId,
                Title: request.Title,
                Summary: request.Summary,
                Content: request.Content,
                Attachments: request.Attachments,
                PostType: request.PostType,
                Status: request.Status
            );

            return await Sender.Send(command);
        }

        /*
        /// <summary>
        /// Updates an existing post.
        /// </summary>
        /// <param name="request">A <see cref="UpdatePostRequest"/> object containing the post ID and updated fields.</param>
        /// <returns>
        /// → <seealso cref="UpdatePostCommand" /><br/>
        /// → <seealso cref="UpdatePostCommandHandler" /><br/>
        /// </returns>
        [HttpPut("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> UpdatePost([FromRoute] Guid postId, [FromBody] UpdatePostRequest request)
        {
            var command = new UpdatePostCommand(
                PostId: postId,
                Title: request.Title,
                Summary: request.Summary,
                Content: request.Content,
                CategoryId: request.CategoryId,
                Status: request.Status
            );

            return await Sender.Send(command);
        }

        /// <summary>
        /// Deletes a post by its ID.
        /// </summary>
        [HttpDelete("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> DeletePost([FromRoute] DeletePostRequest request)
        {
            var command = new DeletePostCommand(PostId: request.PostId);
            return await Sender.Send(command);
        }

        /// <summary>
        /// Retrieves a paginated list of posts with optional filters.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<PostDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<IEnumerable<PostDto>>> GetPosts([FromQuery] GetPostsRequest request)
        {
            var query = new GetPostsQuery(
                AuthorId: request.AuthorId,
                CategoryId: request.CategoryId,
                Status: request.Status,
                SearchKeyword: request.SearchKeyword,
                Limit: request.Limit,
                Offset: request.Offset
            );

            return await Sender.Send(query);
        }

        /// <summary>
        /// Retrieves a single post by its ID.
        /// </summary>
        [HttpGet("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<PostDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<PostDto>> GetPostById([FromRoute] GetPostByIdRequest request)
        {
            var query = new GetPostByIdQuery(PostId: request.PostId);
            return await Sender.Send(query);
        }

        /// <summary>
        /// Increments the view count of a post.
        /// </summary>
        [HttpPut("{postId}/view")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<bool>> IncrementViewCount([FromRoute] IncrementViewCountRequest request)
        {
            var command = new IncrementViewCountCommand(PostId: request.PostId);
            return await Sender.Send(command);
        }

        /// <summary>
        /// Marks or unmarks a post as featured.
        /// </summary>
        [HttpPut("{postId}/feature")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<bool>> ToggleFeatured([FromRoute] Guid postId, [FromBody] ToggleFeaturedRequest request)
        {
            var command = new ToggleFeaturedCommand(PostId: postId, IsFeatured: request.IsFeatured);
            return await Sender.Send(command);
        }

        /// <summary>
        /// Retrieves the total number of posts by a specific author.
        /// </summary>
        [HttpGet("count/by-author")]
        [ProducesResponseType(typeof(BaseResponseDto<int>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<int>> GetPostCountByAuthor([FromQuery] GetPostCountByAuthorRequest request)
        {
            var query = new GetPostCountByAuthorQuery(AuthorId: request.AuthorId);
            return await Sender.Send(query);
        }
        */
    }
}
