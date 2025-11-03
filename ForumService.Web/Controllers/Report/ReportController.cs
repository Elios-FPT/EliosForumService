using Asp.Versioning;
using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Command;
using static ForumService.Contract.UseCases.Report.Request;

namespace ForumService.Web.Controllers.Report
{
    [ApiVersion(1)]
    [Route("api/forum/reports")] 
    [ApiController]
    [Produces("application/json")]
    [ControllerName("Report")]
    public class ReportController : ControllerBase
    {
        protected readonly ISender Sender;

        public ReportController(ISender sender)
        {
            Sender = sender;
        }

        /// <summary>
        /// Submits a new report for a post or comment.
        /// </summary>
        /// <param name="request">The report details.</param>
        /// <returns>The ID of the newly created report.</returns>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(BaseResponseDto<Guid>), StatusCodes.Status500InternalServerError)]
        public async Task<BaseResponseDto<Guid>> CreateReport([FromBody] CreateReportRequest request)
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

            //var userId = new Guid("3ea1d8be-846d-47eb-9961-7f7d32f37ec1");

            try
            {
                var command = new CreateReportCommand(
                    ReporterId: userId,
                    TargetType: request.TargetType,
                    TargetId: request.TargetId,
                    Reason: request.Reason,
                    Details: request.Details
                );

                var result = await Sender.Send(command);

                // Cập nhật StatusCode của response dựa trên kết quả từ handler
                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<Guid>
                {
                    Status = 500,
                    Message = $"Failed to create report: {ex.Message}",
                    ResponseData = Guid.Empty
                };
            }
        }
    }
}
