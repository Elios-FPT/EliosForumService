using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;
using Microsoft.Extensions.Logging; // Added
using ForumService.Contract.TransferObjects; // Added
using System.Text.Json; // Added

namespace ForumService.Core.Handler.Post.Command
{
    public class RejectPostCommandHandler : ICommandHandler<RejectPostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient; // Added
        private readonly ILogger<RejectPostCommandHandler> _logger; // Added

        public RejectPostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient, // Added
            ILogger<RejectPostCommandHandler> logger) // Added
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient)); // Added
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Added
        }

        public async Task<BaseResponseDto<bool>> Handle(RejectPostCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();

            Domain.Models.Post post; // Declared here to be accessible outside the try-catch block

            try
            {
                post = await _postRepository.GetByIdAsync(request.PostId);

                if (post == null || post.IsDeleted)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };
                }

                if (post.Status != "PendingReview")
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 400, Message = $"Only posts with 'PendingReview' status can be rejected. Current status is '{post.Status}'.", ResponseData = false };
                }

                post.Status = "Rejected";
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.ModeratorId;
                post.RejectionReason = request.Reason; // Set the rejection reason
                post.ModeratedBy = request.ModeratorId;

                await _postRepository.UpdateAsync(post);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to reject post: {errorMessage}", ResponseData = false };
            }

            // --- Add notification logic ---
            try
            {
                // Only send notification if the moderator is not the author of the post
                if (post.AuthorId != request.ModeratorId)
                {
                    string title = "Your post has been rejected";
                    string truncatedTitle = $"{post.Title.Substring(0, Math.Min(post.Title.Length, 50))}{(post.Title.Length > 50 ? "..." : "")}";

                    // Include the rejection reason in the message
                    string message = $"Your post \"{truncatedTitle}\" was rejected. Reason: {request.Reason}";

                    var metadataDict = new Dictionary<string, string>
                    {
                        { "PostId", post.PostId.ToString() },
                        { "TriggeredByUserId", request.ModeratorId.ToString() },
                        { "Reason", request.Reason } 
                    };

                    var notificationRequest = new NotificationDto
                    {
                        UserId = post.AuthorId, // Send to the post author
                        Title = title,
                        Message = message,
                        Url = $"/posts/{post.PostId}", // Link back to the (now rejected) post
                        Metadata = JsonSerializer.Serialize(metadataDict)
                    };

                    await _utilityServiceClient.SendNotificationAsync(notificationRequest, cancellationToken);
                }
            }
            catch (Exception notifyEx)
            {
                // Log the error but don't return failure to the client, 
                // as the main action (rejecting post) succeeded
                _logger.LogError(notifyEx, "Successfully rejected post {PostId} but failed to send notification.", post.PostId);
            }
            // --- End notification logic ---

            return new BaseResponseDto<bool> { Status = 200, Message = "Post rejected successfully.", ResponseData = true };
        }
    }
}