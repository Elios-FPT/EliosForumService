using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Comment.Command;
using Microsoft.Extensions.Logging; 
using System.Text.Json;
using ForumService.Contract.TransferObjects;

namespace ForumService.Core.Handler.Comment.Command
{

    public class CreateCommentCommandHandler : ICommandHandler<CreateCommentCommand, BaseResponseDto<Guid>>
    {
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient; 
        private readonly ILogger<CreateCommentCommandHandler> _logger; 

        public CreateCommentCommandHandler(
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient, 
            ILogger<CreateCommentCommandHandler> logger) 
        {
            _commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger)); 
        }

        public async Task<BaseResponseDto<Guid>> Handle(CreateCommentCommand request, CancellationToken cancellationToken)
        {
            
            if (request.AuthorId == Guid.Empty)
            {
                return new BaseResponseDto<Guid> { Status = 400, Message = "AuthorId cannot be empty.", ResponseData = Guid.Empty };
            }
            if (request.PostId == Guid.Empty)
            {
                return new BaseResponseDto<Guid> { Status = 400, Message = "PostId cannot be empty.", ResponseData = Guid.Empty };
            }
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<Guid> { Status = 400, Message = "Comment content cannot be empty.", ResponseData = Guid.Empty };
            }

            await _unitOfWork.BeginTransactionAsync();

            Domain.Models.Post post;
            Domain.Models.Comment? parentComment = null; 
            Guid newCommentId;

            try
            {
               
                post = await _postRepository.GetOneAsync(p => p.PostId == request.PostId && p.Status == "Published" && !p.IsDeleted);
                if (post == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<Guid> { Status = 404, Message = "Post not found or is not available for commenting.", ResponseData = Guid.Empty };
                }

                // If it's a reply, validate the parent comment.
                if (request.ParentCommentId.HasValue)
                {
                    
                    parentComment = await _commentRepository.GetOneAsync(c => c.CommentId == request.ParentCommentId.Value && !c.IsDeleted);
                    if (parentComment == null)
                    {
                        await _unitOfWork.RollbackAsync();
                        return new BaseResponseDto<Guid> { Status = 404, Message = "Parent comment not found.", ResponseData = Guid.Empty };
                    }
                    if (parentComment.PostId != request.PostId)
                    {
                        await _unitOfWork.RollbackAsync();
                        return new BaseResponseDto<Guid> { Status = 400, Message = "Reply must belong to the same post as the parent comment.", ResponseData = Guid.Empty };
                    }
                }

                // Create the new comment entity
                var newComment = new Domain.Models.Comment
                {
                    CommentId = Guid.NewGuid(),
                    PostId = request.PostId,
                    ParentCommentId = request.ParentCommentId,
                    AuthorId = request.AuthorId,
                    Content = request.Content,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                newCommentId = newComment.CommentId; 

                await _commentRepository.AddAsync(newComment);

                // Increment the CommentCount on the parent post
                post.CommentCount++;
                await _postRepository.UpdateAsync(post);

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<Guid>
                {
                    Status = 500,
                    Message = $"An error occurred while creating the comment: {ex.Message}",
                    ResponseData = Guid.Empty
                };
            }

        
            try
            {
                Guid? recipientId = null;
                string title = "";
                string message = $"New comment: \"{request.Content.Substring(0, Math.Min(request.Content.Length, 50))}...\"";

                if (parentComment != null)
                {
                    recipientId = parentComment.AuthorId;
                    title = "Someone replied to your comment";
                }
                else
                {
                    recipientId = post.AuthorId;
                    title = "Someone commented on your post";
                }

                if (recipientId.HasValue && recipientId.Value != request.AuthorId)
                {
                    var metadataDict = new Dictionary<string, string>
                    {
                        { "PostId", post.PostId.ToString() },
                        { "CommentId", newCommentId.ToString() },
                        { "TriggeredByUserId", request.AuthorId.ToString() }
                    };

                    var notificationRequest = new NotificationDto
                    {
                        UserId = recipientId.Value,
                        Title = title,
                        Message = message,
                        Url = $"/posts/{post.PostId}?commentId={newCommentId}", 
                        Metadata = JsonSerializer.Serialize(metadataDict) 
                    };

                    await _utilityServiceClient.SendNotificationAsync(notificationRequest, cancellationToken);
                }
            }
            catch (Exception notifyEx)
            {
                _logger.LogError(notifyEx, "Successfully created comment {CommentId} but failed to send notification.", newCommentId);
            }

            return new BaseResponseDto<Guid>
            {
                Status = 201, // 201 Created
                Message = "Comment created successfully.",
                ResponseData = newCommentId
            };
        }
    }
}