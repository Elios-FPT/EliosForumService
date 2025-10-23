using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.UseCases.Post;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class DeletePostCommandHandler : ICommandHandler<DeletePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeletePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                post.IsDeleted = true;
                post.DeletedAt = DateTime.UtcNow;
                post.DeletedBy = request.RequesterId;

              
                await _postRepository.UpdateAsync(post);

                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = "Post deleted successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();

                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to delete post: {errorMessage}", ResponseData = false };
            }
        }
    }
}

