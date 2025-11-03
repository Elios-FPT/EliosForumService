using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Core.Handler.Comment.Command
{
    public class UpdateCommentCommandHandler : ICommandHandler<UpdateCommentCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateCommentCommandHandler(
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IUnitOfWork unitOfWork)
        {
            _commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<bool>> Handle(UpdateCommentCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "Content cannot be empty.", ResponseData = false };
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var comment = await _commentRepository.GetByIdAsync(request.CommentId);

                if (comment == null || comment.IsDeleted)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = "Comment not found.", ResponseData = false };
                }

                // Only the author can edit their own comment.
                if (comment.AuthorId != request.RequesterId)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to edit this comment.", ResponseData = false };
                }

                //  add a time limit for editing (e.g., cannot edit after 30 minutes)
                // if (comment.CreatedAt.AddMinutes(30) < DateTime.UtcNow)
                // {
                //     await _unitOfWork.RollbackAsync();
                //     return new BaseResponseDto<bool> { Status = 403, Message = "Comments can no longer be edited.", ResponseData = false };
                // }

                comment.Content = request.Content;
                comment.UpdatedAt = DateTime.UtcNow;

                await _commentRepository.UpdateAsync(comment);
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Comment updated successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"An error occurred while updating the comment: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}
