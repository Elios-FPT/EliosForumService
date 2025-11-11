using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using System.Threading;
using System.Threading.Tasks;
using System;
using static ForumService.Contract.UseCases.Post.Command;
using Microsoft.Extensions.Logging; // Added
using ForumService.Contract.TransferObjects; // Added
using System.Text.Json; // Added
using System.Collections.Generic; // Added

namespace ForumService.Core.Handler.Post.Command
{
    public class UpvotePostCommandHandler : ICommandHandler<UpvotePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Vote> _voteRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient; // Added
        private readonly ILogger<UpvotePostCommandHandler> _logger; // Added

        public UpvotePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Vote> voteRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient, // Added
            ILogger<UpvotePostCommandHandler> logger) // Added
        {
            _postRepository = postRepository;
            _voteRepository = voteRepository;
            _unitOfWork = unitOfWork;
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient)); // Added
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Added
        }

        public async Task<BaseResponseDto<bool>> Handle(UpvotePostCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();

            Domain.Models.Post post; // Hoist declaration to access it after try-catch
            string successMessage;
            bool shouldNotify = false; // Flag to control notification sending

            try
            {
                post = await _postRepository.GetByIdAsync(request.PostId);

                if (post == null || post.IsDeleted)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };
                }

                // --- BUG FIX ADDED ---
                // Business Rule: Users cannot vote on their own posts.
                if (post.AuthorId == request.RequesterId)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 403, Message = "You cannot vote on your own post.", ResponseData = false };
                }
                // --- END BUG FIX ---

                // Find any existing vote by this user on this post.
                var existingVote = await _voteRepository.GetOneAsync(
                    filter: v => v.TargetId == request.PostId &&
                                  v.UserId == request.RequesterId &&
                                  v.TargetType == "Post"
                );

                // string successMessage; // Hoisted

                if (existingVote == null)
                {
                    // 1. No existing vote: Create a new Upvote.
                    var newVote = new Domain.Models.Vote
                    {
                        VoteId = Guid.NewGuid(),
                        UserId = request.RequesterId,
                        TargetType = "Post",
                        TargetId = request.PostId,
                        VoteType = "Upvote",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _voteRepository.AddAsync(newVote);
                    post.UpvoteCount++;
                    successMessage = "Post upvoted successfully.";
                    shouldNotify = true; // Send notification
                }
                else if (existingVote.VoteType == "Upvote")
                {
                    // 2. Already upvoted: Remove the upvote (toggle).
                    await _voteRepository.DeleteAsync(existingVote);
                    post.UpvoteCount--;
                    successMessage = "Upvote removed.";
                    // shouldNotify remains false
                }
                else // existingVote.VoteType == "Downvote"
                {
                    // 3. Was downvoted: Change the vote to an Upvote.
                    existingVote.VoteType = "Upvote";
                    existingVote.CreatedAt = DateTime.UtcNow; // Update the timestamp
                    await _voteRepository.UpdateAsync(existingVote);

                    post.DownvoteCount--;
                    post.UpvoteCount++;
                    successMessage = "Vote changed to upvote.";
                    shouldNotify = true; // Send notification
                }

                // Update the post's vote count and metadata.
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.RequesterId;
                await _postRepository.UpdateAsync(post);

                await _unitOfWork.CommitAsync();

            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to upvote post: {errorMessage}", ResponseData = false };
            }

            // --- Send Notification (if applicable) ---
            // We only notify if a new upvote was added or changed (shouldNotify = true)
            // and the actor is not the post author (already checked)
            if (shouldNotify && post.AuthorId != request.RequesterId)
            {
                try
                {
                    var title = "Someone upvoted your post";
                    string truncatedTitle = $"{post.Title.Substring(0, Math.Min(post.Title.Length, 50))}{(post.Title.Length > 50 ? "..." : "")}";
                    var message = $"Your post \"{truncatedTitle}\" received an upvote.";

                    var metadataDict = new Dictionary<string, string>
                    {
                        { "PostId", post.PostId.ToString() },
                        { "TriggeredByUserId", request.RequesterId.ToString() }
                    };

                    var notificationRequest = new NotificationDto
                    {
                        UserId = post.AuthorId, // Send to the post author
                        Title = title,
                        Message = message,
                        Url = $"/posts/{post.PostId}",
                        Metadata = JsonSerializer.Serialize(metadataDict)
                    };

                    await _utilityServiceClient.SendNotificationAsync(notificationRequest, cancellationToken);
                }
                catch (Exception notifyEx)
                {
                    // Log the notification error, but don't fail the whole request
                    _logger.LogError(notifyEx, "Successfully upvoted Post {PostId} but failed to send notification.", post.PostId);
                }
            }

            return new BaseResponseDto<bool> { Status = 200, Message = successMessage, ResponseData = true };
        }
    }
}