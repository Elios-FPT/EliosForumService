using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class ApprovePostCommandHandler : ICommandHandler<ApprovePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient;
        private readonly ILogger<ApprovePostCommandHandler> _logger;
        private readonly IKafkaProducer _kafkaProducer;
        private readonly string _topicName;
        private readonly string _currentServiceName;

        public ApprovePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient,
            ILogger<ApprovePostCommandHandler> logger,
            IKafkaProducer kafkaProducer,
            IAppConfiguration appConfig)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
            _currentServiceName = appConfig.GetCurrentServiceName();
            _topicName = $"{_currentServiceName}-user-userstats";
        }

        public async Task<BaseResponseDto<bool>> Handle(ApprovePostCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();

            Domain.Models.Post post;

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
                    return new BaseResponseDto<bool> { Status = 400, Message = $"Only posts with 'PendingReview' status can be approved. Current status is '{post.Status}'.", ResponseData = false };
                }

                post.Status = "Published";
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.ModeratorId;
                post.ModeratedBy = request.ModeratorId;

                await _postRepository.UpdateAsync(post);

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to approve post: {errorMessage}", ResponseData = false };
            }

            // --- 3. KAFKA EVENT LOGIC (Fire and Forget) ---
            try
            {
                var eventPayload = new PostApprovedEvent
                {
                    PostId = post.PostId,
                    UserId = post.AuthorId,
                    ApprovedAt = DateTime.UtcNow
                };

                var wrapper = new EventWrapper(
                    EventType: "POST_APPROVED",
                    ModelType: nameof(PostApprovedEvent),
                    Payload: eventPayload,
                    EventId: Guid.NewGuid().ToString(),
                    CorrelationId: Guid.NewGuid().ToString(),
                    Timestamp: DateTime.UtcNow
                );

                // Serialize Wrapper
                string jsonPayload = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                await _kafkaProducer.ProduceAsync(_topicName, post.AuthorId.ToString(), jsonPayload, cancellationToken);
            }
            catch (Exception kafkaEx)
            {
                _logger.LogError(kafkaEx, "Post {PostId} approved but failed to publish Kafka stats event.", post.PostId);
            }
            // --- End Kafka logic ---


            // --- 4. Notification Logic ---
            try
            {
                if (post.AuthorId != request.ModeratorId)
                {
                    string title = "Your post has been approved";
                    string message = $"Your post \"{post.Title.Substring(0, Math.Min(post.Title.Length, 50))}{(post.Title.Length > 50 ? "..." : "")}\" has been published.";

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
                _logger.LogError(notifyEx, "Successfully approved post {PostId} but failed to send notification.", post.PostId);
            }
            // --- End notification logic ---

            return new BaseResponseDto<bool> { Status = 200, Message = "Post approved and published successfully.", ResponseData = true };
        }
    }
}