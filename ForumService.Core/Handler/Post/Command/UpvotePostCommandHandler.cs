using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using System.Threading;
using System.Threading.Tasks;
using System;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class UpvotePostCommandHandler : ICommandHandler<UpvotePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Vote> _voteRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpvotePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Vote> voteRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _voteRepository = voteRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(UpvotePostCommand request, CancellationToken cancellationToken)
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

                // Find any existing vote by this user on this post.
                var existingVote = await _voteRepository.GetOneAsync(
                    filter: v => v.TargetId == request.PostId &&
                                 v.UserId == request.RequesterId &&
                                 v.TargetType == "Post"
                );

                string successMessage;

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
                }
                else if (existingVote.VoteType == "Upvote")
                {
                    // 2. Already upvoted: Remove the upvote (toggle).
                    await _voteRepository.DeleteAsync(existingVote);
                    post.UpvoteCount--;
                    successMessage = "Upvote removed.";
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
                }

                // Update the post's vote count and metadata.
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.RequesterId;
                await _postRepository.UpdateAsync(post);

                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = successMessage, ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to upvote post: {errorMessage}", ResponseData = false };
            }
        }
    }
}
