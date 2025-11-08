using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    /// <summary>
    /// Handles the logic for a Moderator deleting a post and sending a notification.
    /// </summary>
    public class ModeratorDeletePostCommandHandler : ICommandHandler<ModeratorDeletePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient;
        private readonly ILogger<ModeratorDeletePostCommandHandler> _logger;

        public ModeratorDeletePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient,
            ILogger<ModeratorDeletePostCommandHandler> logger)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BaseResponseDto<bool>> Handle(ModeratorDeletePostCommand request, CancellationToken cancellationToken)
        {
            Domain.Models.Post post;
            try
            {
                post = await _postRepository.GetByIdAsync(request.PostId);

                if (post == null || post.IsDeleted)
                {
                    return new BaseResponseDto<bool> { Status = 404, Message = "Post not found or has already been deleted.", ResponseData = false };
                }


                await _unitOfWork.BeginTransactionAsync();

                post.IsDeleted = true;
                post.DeletedAt = DateTime.UtcNow;
                post.DeletedBy = request.ModeratorId;
                await _postRepository.UpdateAsync(post);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to delete post: {errorMessage}", ResponseData = false };
            }

            try
            {
                if (post.AuthorId != request.ModeratorId)
                {
                    var title = "Your post has been removed by a moderator";
                    var message = $"Your post \"{post.Title.Substring(0, Math.Min(post.Title.Length, 50))}...\" was removed. Reason: {request.Reason}";

                    var metadataDict = new Dictionary<string, string>
                    {
                        { "PostId", post.PostId.ToString() },
                        { "TriggeredByUserId", request.ModeratorId.ToString() }
                    };

                    var notificationRequest = new NotificationDto
                    {
                        UserId = post.AuthorId,
                        Title = title,
                        Message = message,
                        Url = $"/posts/{post.PostId}",
                        Metadata = JsonSerializer.Serialize(metadataDict)
                    };

                    await _utilityServiceClient.SendNotificationAsync(notificationRequest, cancellationToken);
                }
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "Successfully deleted post {PostId} but failed to send notification.", post.PostId);
            }

            return new BaseResponseDto<bool> { Status = 200, Message = "Post deleted successfully by moderator.", ResponseData = true };
        }
    }
}
