using Asp.Versioning;
using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static ForumService.Contract.UseCases.Comment.Command;
using static ForumService.Contract.UseCases.Comment.Request;

namespace ForumService.Web.Controllers.Comment
{
    [ApiVersion(1)]
    [Route("api/forum/[controller]")]
    [ApiController]
    [Produces("application/json")]
    [ControllerName("Comment")]
    public class CommentController : ControllerBase
    {
        protected readonly ISender Sender;

        public CommentController(ISender sender)
        {
            Sender = sender;
        }

        /// <summary>
        /// Creates a new comment on a post or replies to an existing comment.
        /// </summary>
        /// <param name="request">The comment details.</param>
        /// <returns>The ID of the newly created comment.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status201Created)] 
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status400BadRequest)] 
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status401Unauthorized)] 
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status404NotFound)] 
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status500InternalServerError)] 
        public async Task<BaseResponseDto<Guid>> CreateComment([FromBody] CreateCommentRequest request)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<Guid>
                {
                    Status = 401,
                    Message = "User not authenticated or invalid/missing X-Auth-Request-User header",
                    ResponseData = Guid.Empty
                };
            }
            //var userId = new Guid("cc51ccca-fa67-4cbe-91df-122e8ea33ac9");
            try
            {
                var command = new CreateCommentCommand(
                    request.PostId,
                    request.ParentCommentId,
                    userId, 
                    request.Content
                );

                var result = await Sender.Send(command);

                if (result == null)
                {
                    return new BaseResponseDto<Guid>
                    {
                        Status = 500,
                        Message = "Failed to create comment: Handler returned null.",
                        ResponseData = Guid.Empty
                    };
                }

                HttpContext.Response.StatusCode = result.Status; 
                return result;

            }
            catch (Exception ex) 
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<Guid>
                {
                    Status = 500,
                    Message = $"Failed to create comment: {ex.Message}",
                    ResponseData = Guid.Empty
                };
            }
        }

        /// <summary>
        /// Updates an existing comment.
        /// </summary>
        /// <param name="commentId">The ID of the comment to update.</param>
        /// <param name="request">The request containing the new content.</param>
        /// <returns>A boolean indicating success.</returns>
        [HttpPut("{commentId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> UpdateComment([FromRoute] Guid commentId, [FromBody] UpdateCommentRequest request)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 401,
                    Message = "User not authenticated or invalid/missing X-Auth-Request-User header",
                    ResponseData = false
                };
            }

            //var userId = new Guid("cc51ccca-fa67-4cbe-91df-122e8ea33ac9");

            try
            {
                var command = new UpdateCommentCommand(
                    CommentId: commentId,
                    RequesterId: userId,
                    Content: request.Content
                );

                var result = await Sender.Send(command);
                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to update comment: {ex.Message}",
                    ResponseData = false
                };
            }
        }

        /// <summary>
        /// Deletes a comment (soft delete).
        /// </summary>
        /// <param name="commentId">The ID of the comment to delete.</param>
        /// <returns>A boolean indicating success.</returns>
        [HttpDelete("{commentId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> DeleteComment([FromRoute] Guid commentId)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 401,
                    Message = "User not authenticated or invalid/missing X-Auth-Request-User header",
                    ResponseData = false
                };
            }

            //var userId = new Guid("cc51ccca-fa67-4cbe-91df-122e8ea33ac9");

            try
            {
                var command = new DeleteCommentCommand(
                    CommentId: commentId,
                    RequesterId: userId
                );

                var result = await Sender.Send(command);
                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to delete comment: {ex.Message}",
                    ResponseData = false
                };
            }
        }

    }
}
