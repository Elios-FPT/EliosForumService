using Asp.Versioning;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Contract.TransferObjects.Report;
using ForumService.Web.Attributes;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Command;
using static ForumService.Contract.UseCases.Report.Query;
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
        [ServiceAuthorize("User")]
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

        /// <summary>
        /// Retrieves a list of reports with pagination and filtering. Accessible by Content Moderators.
        /// </summary>
        [HttpGet]
        [ServiceAuthorize("Content Moderator")]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<ReportDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<ReportDto>>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<ReportDto>>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<ReportDto>>), StatusCodes.Status500InternalServerError)]
        public async Task<BaseResponseDto<IEnumerable<ReportDto>>> GetReports([FromQuery] GetReportsRequest request)
        {
            try
            {
                var query = new GetReportsQuery(
                    Offset: request.Offset,
                    Limit: request.Limit,
                    Status: request.Status,
                    TargetType: request.TargetType,
                    ReporterId: request.ReporterId,
                    TargetId: request.TargetId,
                    SortBy: request.SortBy,
                    SortOrder: request.SortOrder
                );

                var result = await Sender.Send(query);

                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<IEnumerable<ReportDto>>
                {
                    Status = 500,
                    Message = $"Failed to retrieve reports: {ex.Message}",
                    ResponseData = Enumerable.Empty<ReportDto>()
                };
            }
        }

        [HttpGet("{reportId}")]
        [ServiceAuthorize("Content Moderator")]
        public async Task<BaseResponseDto<ReportDto>> GetReportById(Guid reportId)
        {
            try
            {
                var query = new GetReportByIdQuery(reportId);
                var result = await Sender.Send(query);
                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<ReportDto> { Status = 500, Message = ex.Message, ResponseData = null };
            }
        }
        /// <summary>
        /// Resolve a report (Reject report OR Accept & Optionally Delete content). Accessible by Content Moderators.
        /// </summary>
        [HttpPut("{reportId}/resolve")]
        [ServiceAuthorize("Content Moderator")]
        public async Task<BaseResponseDto<bool>> ResolveReport(Guid reportId, [FromBody] ResolveReportRequest request)
        {
            var moderatorIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(moderatorIdHeader) || !Guid.TryParse(moderatorIdHeader, out var moderatorId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "Moderator not authenticated", ResponseData = false };
            }

            //var moderatorId = new Guid("902ea1b3-f664-4617-8f43-fdde557f12b6");

            try
            {
                var command = new ResolveReportCommand(
                    ReportId: reportId,
                    ModeratorId: moderatorId,
                    Status: request.Status,
                    DeleteContent: request.DeleteContent, 
                    ModeratorNote: request.ModeratorNote
                );

                var result = await Sender.Send(command);
                HttpContext.Response.StatusCode = result.Status;
                return result;
            }
            catch (Exception ex)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return new BaseResponseDto<bool> { Status = 500, Message = ex.Message, ResponseData = false };
            }
        }
    }
}
