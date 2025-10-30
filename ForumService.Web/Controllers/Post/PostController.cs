using Asp.Versioning;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Contract.TransferObjects.Post;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static ForumService.Contract.UseCases.Post.Command;
using static ForumService.Contract.UseCases.Post.Query;
using static ForumService.Contract.UseCases.Post.Request;
using System.IO; 

namespace ForumService.Web.Controllers.Post
{
    /// <summary>
    /// Post management endpoints.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Post")]
    [Route("api/v1/posts")] 
    public class PostController : ControllerBase
    {
        protected readonly ISender _sender;

        public PostController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Creates a new post with optional file attachments.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<bool>> CreatePost([FromForm] CreatePostRequest request, List<IFormFile> files)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }

            //var userId = new Guid("102ea1b3-f664-4617-8f43-fdde557f12b6");

            var filesToUpload = new List<FileToUploadDto>();
            if (files is not null)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);
                        filesToUpload.Add(new FileToUploadDto
                        {
                            FileName = file.FileName,
                            ContentType = file.ContentType,
                            Content = memoryStream.ToArray()
                        });
                    }
                }
            }

            var command = new CreatePostCommand(
                AuthorId: userId,
                CategoryId: request.CategoryId,
                Title: request.Title,
                Content: request.Content,
                PostType: request.PostType,
                FilesToUpload: filesToUpload
            );
            return await _sender.Send(command);
        }

        /// <summary>
        /// Updates an existing post and its file attachments.
        /// </summary>
        [HttpPut("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        public async Task<BaseResponseDto<bool>> UpdatePost([FromRoute] Guid postId, [FromForm] UpdatePostRequest request, List<IFormFile> files)
        {
          
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }
       
            var newFilesToUpload = new List<FileToUploadDto>();
            if (files is not null)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);
                        newFilesToUpload.Add(new FileToUploadDto
                        {
                            FileName = file.FileName,
                            ContentType = file.ContentType,
                            Content = memoryStream.ToArray()
                        });
                    }
                }
            }
            var command = new UpdatePostCommand(
                PostId: postId,
                RequesterId: userId, 
                Title: request.Title,
                Summary: request.Summary,
                Content: request.Content,
                CategoryId: request.CategoryId,
                NewFilesToUpload: newFilesToUpload,
                AttachmentIdsToDelete: request.AttachmentIdsToDelete
            );
            return await _sender.Send(command);
        }

        /// <summary>
        /// Retrieves a paginated list of PUBLISHED posts.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<PostViewDto>>), StatusCodes.Status200OK)]
        public async Task<BaseResponseDto<IEnumerable<PostViewDto>>> GetPublicViewPosts([FromQuery] GetPublishedPostsRequest request)
        {

            var query = new GetPublicViewPostsQuery(
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
        /// Retrieves the detailed view of a single published post by its ID.
        /// </summary>
        /// <param name="postId">The ID of the post to retrieve.</param>
        /// <returns>The detailed information of the post.</returns>
        [HttpGet("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<PostViewDetailDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<PostViewDetailDto>> GetPostDetailsById([FromRoute] Guid postId)
        {
           
            var query = new GetPostDetailsByIdQuery(postId);
            return await _sender.Send(query);
        }

        /// <summary>
        /// Retrieves all posts created by the current authenticated user (excluding soft-deleted posts).
        /// </summary>
        /// <remarks>
        /// This endpoint allows users to see their own posts across all statuses (Draft, PendingReview, Published, Rejected).
        /// It supports filtering and pagination.
        /// </remarks>
        [HttpGet("my-posts")]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<PostViewDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<BaseResponseDto<IEnumerable<PostViewDto>>> GetMyPosts([FromQuery] GetMyPostsRequest request)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 401,
                    Message = "User not authenticated or invalid/missing X-Auth-Request-User header",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }

            //var userId = new Guid("102ea1b3-f664-4617-8f43-fdde557f12b6");

            var query = new GetMyPostsQuery(
                RequesterId: userId,
                Status: request.Status,
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
        /// Deletes a post by its ID. (Soft delete)
        /// </summary>
        [HttpDelete("{postId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        public async Task<BaseResponseDto<bool>> DeletePost([FromRoute] Guid postId)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }

            //var userId = new Guid("102ea1b3-f664-4617-8f43-fdde557f12b6");

            var command = new DeletePostCommand(PostId: postId, RequesterId: userId);
            return await _sender.Send(command);
        }

        /// <summary>
        /// Submits a draft post for review and associates tags with it.
        /// </summary>
        [HttpPut("{postId}/submit")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)] 
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> SubmitPostForReview(
            [FromRoute] Guid postId,
            [FromBody] SubmitPostForReviewRequest request)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }

            if (request == null)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Invalid request body. Ensure Content-Type is application/json and body matches expected format: { \"tags\": [\"tag1\", \"tag2\"] } or { \"tags\": null } or {}", 
                    ResponseData = false
                };
            }

            //var userId = new Guid("102ea1b3-f664-4617-8f43-fdde557f12b6");
       
            var command = new SubmitPostForReviewCommand(postId, userId, request.Tags);
            return await _sender.Send(command);
        }


    }
}

