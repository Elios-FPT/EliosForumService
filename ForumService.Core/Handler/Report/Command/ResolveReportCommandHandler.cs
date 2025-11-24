using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Command;

namespace ForumService.Core.Handler.Report.Command
{
    public class ResolveReportCommandHandler : ICommandHandler<ResolveReportCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Report> _reportRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient; 
        private readonly ILogger<ResolveReportCommandHandler> _logger; 

        public ResolveReportCommandHandler(
            IGenericRepository<Domain.Models.Report> reportRepository,
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient,
            ILogger<ResolveReportCommandHandler> logger)
        {
            _reportRepository = reportRepository;
            _postRepository = postRepository;
            _commentRepository = commentRepository;
            _unitOfWork = unitOfWork;
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BaseResponseDto<bool>> Handle(ResolveReportCommand request, CancellationToken cancellationToken)
        {
            // 1. Validate Input Status
            if (request.Status != "Approved" && request.Status != "Rejected")
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "Status must be 'Approved' or 'Rejected'.", ResponseData = false };
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 2. Get Report
                var report = await _reportRepository.GetByIdAsync(request.ReportId);
                if (report == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = "Report not found.", ResponseData = false };
                }

                if (report.Status != "Pending")
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 400, Message = "This report has already been processed.", ResponseData = false };
                }

                // 3. Handle "Delete Violating Content" Logic
                if (request.Status == "Approved" && request.DeleteContent)
                {
                    if (report.TargetType == "Post")
                    {
                        var post = await _postRepository.GetByIdAsync(report.TargetId);
                        if (post != null && !post.IsDeleted)
                        {
                            post.IsDeleted = true;
                            post.UpdatedAt = DateTime.UtcNow;
                            post.DeletedAt = DateTime.UtcNow;
                            await _postRepository.UpdateAsync(post);
                        }
                    }
                    else if (report.TargetType == "Comment")
                    {
                        var comment = await _commentRepository.GetByIdAsync(report.TargetId);
                        if (comment != null && !comment.IsDeleted)
                        {
                            comment.IsDeleted = true;
                            comment.UpdatedAt = DateTime.UtcNow;
                            comment.DeletedAt = DateTime.UtcNow;
                            await _commentRepository.UpdateAsync(comment);
                        }
                    }
                }

                // 4. Update Report Status
                report.Status = request.Status;
                report.ResolvedBy = request.ModeratorId;
                report.ModeratorNote = request.ModeratorNote;
                report.ResolvedAt = DateTime.UtcNow;

                await _reportRepository.UpdateAsync(report);
                await _unitOfWork.CommitAsync();

                _ = SendNotificationAsync(report, request.ModeratorNote, cancellationToken);

                string actionMessage = "Report processed successfully.";
                if (request.Status == "Approved")
                {
                    actionMessage = request.DeleteContent ? "Report resolved and content deleted." : "Report resolved (content kept).";
                }
                else
                {
                    actionMessage = "Report rejected.";
                }

                return new BaseResponseDto<bool> { Status = 200, Message = actionMessage, ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool> { Status = 500, Message = $"Error resolving report: {ex.Message}", ResponseData = false };
            }
        }

       
        private async Task SendNotificationAsync(Domain.Models.Report report, string? moderatorNote, CancellationToken token)
        {
            try
            {
                string title = "Report Status Update";
                string actionText = report.Status == "Resolved" ? "accepted" : "rejected";
                string message = $"Your report regarding a {report.TargetType} has been {actionText}.";

                var metadataDict = new Dictionary<string, string>
                {
                    { "ReportId", report.ReportId.ToString() },
                    { "TargetId", report.TargetId.ToString() },
                    { "TargetType", report.TargetType },
                    { "Status", report.Status }
                };

                var notificationRequest = new NotificationDto
                {
                    UserId = report.ReporterId, 
                    Title = title,
                    Message = message,
                    Url = $"/user/reports", 
                    Metadata = JsonSerializer.Serialize(metadataDict)
                };

                await _utilityServiceClient.SendNotificationAsync(notificationRequest, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for report resolution {ReportId}", report.ReportId);
            }
        }
    }
}
