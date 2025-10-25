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
    [Route("api/v1/[controller]")]
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
            //var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            //if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            //{
            //    return new BaseResponseDto<Guid>
            //    {
            //        Status = 401,
            //        Message = "User not authenticated or invalid/missing X-Auth-Request-User header",
            //        ResponseData = Guid.Empty
            //    };
            //}

            var userId = new Guid("cc51ccca-fa67-4cbe-91df-122e8ea33ac9");

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
    }
}
