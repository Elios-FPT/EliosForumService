using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class RejectPostCommandHandler : ICommandHandler<RejectPostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;

        public RejectPostCommandHandler(IGenericRepository<Domain.Models.Post> postRepository, IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(RejectPostCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var post = await _postRepository.GetByIdAsync(request.PostId);

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


                await _postRepository.UpdateAsync(post);
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = "Post rejected successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to reject post: {errorMessage}", ResponseData = false };
            }
        }
    }
}
