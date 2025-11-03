using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Command;

namespace ForumService.Core.Handler.Report.Command
{
    public class CreateReportCommandHandler : ICommandHandler<CreateReportCommand, BaseResponseDto<Guid>>
    {
        private readonly IGenericRepository<Domain.Models.Report> _reportRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateReportCommandHandler(
            IGenericRepository<Domain.Models.Report> reportRepository,
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IUnitOfWork unitOfWork)
        {
            _reportRepository = reportRepository ?? throw new ArgumentNullException(nameof(reportRepository));
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<Guid>> Handle(CreateReportCommand request, CancellationToken cancellationToken)
        {
            // --- 1. Validation (Input) ---
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return new BaseResponseDto<Guid> { Status = 400, Message = "Reason cannot be empty.", ResponseData = Guid.Empty };
            }

            if (request.TargetType != "Post" && request.TargetType != "Comment")
            {
                return new BaseResponseDto<Guid> { Status = 400, Message = "TargetType must be 'Post' or 'Comment'.", ResponseData = Guid.Empty };
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // --- 2. Validation (Business Logic) ---
                Guid targetAuthorId = Guid.Empty;

                if (request.TargetType == "Post")
                {
                    var post = await _postRepository.GetByIdAsync(request.TargetId);
                    if (post == null || post.IsDeleted)
                    {
                        await _unitOfWork.RollbackAsync();
                        return new BaseResponseDto<Guid> { Status = 404, Message = "Post not found.", ResponseData = Guid.Empty };
                    }
                    targetAuthorId = post.AuthorId;
                }
                else // TargetType is "Comment"
                {
                    var comment = await _commentRepository.GetByIdAsync(request.TargetId);
                    if (comment == null || comment.IsDeleted)
                    {
                        await _unitOfWork.RollbackAsync();
                        return new BaseResponseDto<Guid> { Status = 404, Message = "Comment not found.", ResponseData = Guid.Empty };
                    }
                    targetAuthorId = comment.AuthorId;
                }

                // Check for self-reporting
                if (targetAuthorId == request.ReporterId)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<Guid> { Status = 403, Message = "You cannot report your own content.", ResponseData = Guid.Empty };
                }

                // Check for duplicate pending report
                var existingReport = await _reportRepository.GetOneAsync(
                    filter: r => r.ReporterId == request.ReporterId &&
                                 r.TargetId == request.TargetId &&
                                 r.Status == "Pending"
                );

                if (existingReport != null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<Guid> { Status = 409, Message = "You have already submitted a pending report for this content.", ResponseData = Guid.Empty };
                }

                // --- 3. Action ---
                var newReport = new Domain.Models.Report
                {
                    ReportId = Guid.NewGuid(),
                    ReporterId = request.ReporterId,
                    TargetType = request.TargetType,
                    TargetId = request.TargetId,
                    Reason = request.Reason,
                    Details = request.Details,
                    Status = "Pending", 
                    CreatedAt = DateTime.UtcNow
                };

                await _reportRepository.AddAsync(newReport);
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<Guid>
                {
                    Status = 201, // Created
                    Message = "Report submitted successfully.",
                    ResponseData = newReport.ReportId
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<Guid>
                {
                    Status = 500,
                    Message = $"An error occurred while submitting the report: {ex.Message}",
                    ResponseData = Guid.Empty
                };
            }
        }
    }
}
