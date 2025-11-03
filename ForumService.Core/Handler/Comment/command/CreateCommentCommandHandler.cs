using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Core.Handler.Comment.Command
{

    public class CreateCommentCommandHandler : ICommandHandler<CreateCommentCommand, BaseResponseDto<Guid>>
    {
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateCommentCommandHandler(
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork)
        {
            _commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
            try
            {

                // Find the target post. It must be Published and not deleted.
                var post = await _postRepository.GetOneAsync(p => p.PostId == request.PostId && p.Status == "Published" && !p.IsDeleted);
                if (post == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<Guid> { Status = 404, Message = "Post not found or is not available for commenting.", ResponseData = Guid.Empty };
                }

                // If it's a reply, validate the parent comment.
                if (request.ParentCommentId.HasValue)
                {
                    var parentComment = await _commentRepository.GetOneAsync(c => c.CommentId == request.ParentCommentId.Value && !c.IsDeleted);
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

                await _commentRepository.AddAsync(newComment);

                // Increment the CommentCount on the parent post
                post.CommentCount++;
                await _postRepository.UpdateAsync(post);

                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<Guid>
                {
                    Status = 201,
                    Message = "Comment created successfully.",
                    ResponseData = newComment.CommentId
                };
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
        }
    }
}
