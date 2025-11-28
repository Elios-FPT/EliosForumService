using Asp.Versioning;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.BanUser;
using ForumService.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.BanUser.Command;
using static ForumService.Contract.UseCases.BanUser.Query;
using static ForumService.Contract.UseCases.BanUser.Request;

namespace ForumService.Web.Controllers.BanUser
{
    [ApiVersion(1)]
    [Route("api/forum/bans")]
    [ApiController]
    [Produces("application/json")]
    [ControllerName("Ban")]
    public class ForumUserBanController : ControllerBase
    {
        protected readonly ISender Sender;

        public ForumUserBanController(ISender sender)
        {
            Sender = sender;
        }

        /// <summary>
        /// Bans a user from the forum. Accessible by Admin and Content Moderator.
        /// </summary>
        /// <param name="request">The ban details including UserId, Reason and Duration.</param>
        /// <returns>The ID of the ban record.</returns>
        [HttpPost]
        [ServiceAuthorize("Admin", "Content Moderator")] // Adjust roles as per your system
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status500InternalServerError)]
        public async Task<BaseResponseDto<Guid>> BanUser([FromBody] CreateBanUserRequest request)
        {
            // Extract Admin/Mod ID from Header
            var executorIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(executorIdHeader) || !Guid.TryParse(executorIdHeader, out var executorId))
            {
                return new BaseResponseDto<Guid>
                {
                    Status = 401,
                    Message = "User not authenticated or invalid/missing X-Auth-Request-User header",
                    ResponseData = Guid.Empty
                };
            }

            try
            {
                var command = new CreateBanUserCommand(
                    UserId: request.UserId,
                    Reason: request.Reason,
                    BannedBy: executorId,
                    BanUntil: request.BanUntil
                );

                var result = await Sender.Send(command);

                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<Guid>
                {
                    Status = 500,
                    Message = $"Failed to ban user: {ex.Message}",
                    ResponseData = Guid.Empty
                };
            }
        }

        /// <summary>
        /// Retrieves a paginated list of banned users. Accessible by Admin and Content Moderator.
        /// </summary>
        [HttpGet]
        [ServiceAuthorize("Admin", "Content Moderator")]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<BanDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<BanDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<BaseResponseDto<IEnumerable<BanDto>>> GetBannedUsers([FromQuery] GetBannedUsersRequest request)
        {
            try
            {
                var query = new GetBannedUsersQuery(
                    UserId: request.UserId,
                    IsActive: request.IsActive,
                    Limit: request.Limit,
                    Offset: request.Offset
                );

                var result = await Sender.Send(query);
                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<IEnumerable<BanDto>>
                {
                    Status = 500,
                    Message = $"Error retrieving bans: {ex.Message}",
                    ResponseData = Enumerable.Empty<BanDto>()
                };
            }
        }

        /// <summary>
        /// Retrieves details of a specific ban record by ID.
        /// </summary>
        [HttpGet("{banId}")]
        [ServiceAuthorize("Admin", "Content Moderator")]
        [ProducesResponseType(typeof(BaseResponseDto<BanDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseDto<BanDto>), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<BanDto>> GetBanById(Guid banId)
        {
            try
            {
                var query = new GetBanByIdQuery(banId);
                var result = await Sender.Send(query);
                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<BanDto>
                {
                    Status = 500,
                    Message = $"Internal Server Error: {ex.Message}",
                    ResponseData = null
                };
            }
        }

        /// <summary>
        /// Unbans a user before the expiration date.
        /// </summary>
        [HttpPut("{banId}/unban")]
        [ServiceAuthorize("Admin", "Content Moderator")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> UnbanUser(Guid banId, [FromBody] UnbanUserRequest request)
        {
            var executorIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(executorIdHeader) || !Guid.TryParse(executorIdHeader, out var executorId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "Unauthorized", ResponseData = false };
            }

            try
            {
                var command = new UnbanUserCommand(
                    BanId: banId,
                    UnbannedBy: executorId,
                    UnbanReason: request.UnbanReason
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
                    Message = $"Failed to unban user: {ex.Message}",
                    ResponseData = false
                };
            }
        }

        /// <summary>
        /// Updates an existing ban (e.g., change reason or extend duration).
        /// </summary>
        [HttpPut("{banId}")]
        [ServiceAuthorize("Admin", "Content Moderator")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> UpdateBan(Guid banId, [FromBody] UpdateBanRequest request)
        {
            var executorIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(executorIdHeader) || !Guid.TryParse(executorIdHeader, out var executorId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "Unauthorized", ResponseData = false };
            }

            try
            {
                var command = new UpdateBanCommand(
                    BanId: banId,
                    UpdatedBy: executorId,
                    Reason: request.Reason,
                    BanUntil: request.BanUntil
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
                    Message = $"Failed to update ban: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}
