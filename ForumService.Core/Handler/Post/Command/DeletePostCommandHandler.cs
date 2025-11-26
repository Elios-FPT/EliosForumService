using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Contract.UseCases.Post;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using ForumService.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class DeletePostCommandHandler : ICommandHandler<DeletePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;

        private readonly IKafkaProducer _kafkaProducer;
        private readonly ILogger<DeletePostCommandHandler> _logger;
        private readonly string _topicName;

        public DeletePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork,
            IKafkaProducer kafkaProducer,
            IAppConfiguration appConfig,
            ILogger<DeletePostCommandHandler> logger)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            string currentServiceName = appConfig.GetCurrentServiceName();
            _topicName = $"{currentServiceName}-user-userstats";
        }

        public async Task<BaseResponseDto<bool>> Handle(DeletePostCommand request, CancellationToken cancellationToken)
        {
            var post = await _postRepository.GetByIdAsync(request.PostId);

            if (post == null || post.IsDeleted)
            {
                return new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };
            }

            if (post.AuthorId != request.RequesterId)
            {
                return new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to delete this post.", ResponseData = false };
            }

            bool wasPublished = post.Status == "Published";

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                post.IsDeleted = true;
                post.DeletedAt = DateTime.UtcNow;
                post.DeletedBy = request.RequesterId;

                await _postRepository.UpdateAsync(post);
                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to delete post: {errorMessage}", ResponseData = false };
            }

            // --- KAFKA EVENT LOGIC  ---
            if (wasPublished)
            {
                try
                {
                    var eventPayload = new PostDeletedEvent
                    {
                        PostId = post.PostId,
                        UserId = post.AuthorId,
                        DeletedAt = DateTime.UtcNow
                    };

                    var wrapper = new EventWrapper(
                        EventType: "POST_DELETED",
                        ModelType: nameof(PostDeletedEvent),
                        Payload: eventPayload,
                        EventId: Guid.NewGuid().ToString(),
                        CorrelationId: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow
                    );

                    string jsonPayload = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    _logger.LogInformation("Preparing to send Kafka event. Payload: {JsonPayload}", jsonPayload);

                    await _kafkaProducer.ProduceAsync(_topicName, post.AuthorId.ToString(), jsonPayload, cancellationToken);

                    _logger.LogInformation("Sent POST_DELETED event for post {PostId}", post.PostId);
                }
                catch (Exception kafkaEx)
                {
                    _logger.LogError(kafkaEx, "Post {PostId} deleted but failed to send stats event.", post.PostId);
                }
            }
            // --- END KAFKA EVENT LOGIC  ---

            return new BaseResponseDto<bool> { Status = 200, Message = "Post deleted successfully.", ResponseData = true };
        }
    }
}

