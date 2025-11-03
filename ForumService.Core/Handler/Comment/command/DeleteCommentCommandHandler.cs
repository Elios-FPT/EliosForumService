using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System.Collections.Generic; 
using System.Linq; 
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Core.Handler.Comment.Command
{
    public class DeleteCommentCommandHandler : ICommandHandler<DeleteCommentCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteCommentCommandHandler(
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork)
        {
            _commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<bool>> Handle(DeleteCommentCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var commentToDelete = await _commentRepository.GetByIdAsync(request.CommentId);

                if (commentToDelete == null || commentToDelete.IsDeleted)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = "Comment not found.", ResponseData = false };
                }

                var post = await _postRepository.GetByIdAsync(commentToDelete.PostId);
                if (post == null)
                {
                    // This should technically not happen if data is consistent
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = "Parent post not found.", ResponseData = false };
                }

                // Only the comment author or the post author can delete.
                if (commentToDelete.AuthorId != request.RequesterId && post.AuthorId != request.RequesterId)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to delete this comment.", ResponseData = false };
                }

                // We will recursively soft-delete the comment and all its replies
                int deletedCount = 0;
                var queue = new Queue<Guid>();
                queue.Enqueue(commentToDelete.CommentId);

                while (queue.Any())
                {
                    var currentCommentId = queue.Dequeue();
                    var currentComment = await _commentRepository.GetByIdAsync(currentCommentId);

                    if (currentComment == null || currentComment.IsDeleted)
                    {
                        continue;
                    }

                    // Soft delete the comment
                    currentComment.IsDeleted = true;
                    currentComment.DeletedAt = DateTime.UtcNow;
                    await _commentRepository.UpdateAsync(currentComment);
                    deletedCount++;

                    // Find and enqueue all direct, non-deleted replies
                    var replies = await _commentRepository.GetListAsync(
                        filter: c => c.ParentCommentId == currentCommentId && !c.IsDeleted
                    );

                    foreach (var reply in replies)
                    {
                        queue.Enqueue(reply.CommentId);
                    }
                }

                // Decrement the post's comment count
                if (deletedCount > 0)
                {
                    post.CommentCount -= deletedCount;
                    if (post.CommentCount < 0) post.CommentCount = 0; 
                    post.UpdatedAt = DateTime.UtcNow;
                    post.UpdatedBy = request.RequesterId;
                    await _postRepository.UpdateAsync(post);
                }

                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = $"Comment and {deletedCount - 1} replies deleted successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"An error occurred while deleting the comment: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}
