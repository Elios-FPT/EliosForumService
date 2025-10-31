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
    public class DownvotePostCommandHandler : ICommandHandler<DownvotePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Vote> _voteRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DownvotePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Vote> voteRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _voteRepository = voteRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(DownvotePostCommand request, CancellationToken cancellationToken)
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

                // Business Rule: Users cannot vote on their own posts.
                if (post.AuthorId == request.RequesterId)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 403, Message = "You cannot vote on your own post.", ResponseData = false };
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
                    // 1. No existing vote: Create a new Downvote.
                    var newVote = new Domain.Models.Vote
                    {
                        VoteId = Guid.NewGuid(),
                        UserId = request.RequesterId,
                        TargetType = "Post",
                        TargetId = request.PostId,
                        VoteType = "Downvote",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _voteRepository.AddAsync(newVote);
                    post.DownvoteCount++;
                    successMessage = "Post downvoted successfully.";
                }
                else if (existingVote.VoteType == "Downvote")
                {
                    // 2. Already downvoted: Remove the downvote (toggle).
                    await _voteRepository.DeleteAsync(existingVote);
                    post.DownvoteCount--;
                    successMessage = "Downvote removed.";
                }
                else // existingVote.VoteType == "Upvote"
                {
                    // 3. Was upvoted: Change the vote to a Downvote.
                    existingVote.VoteType = "Downvote";
                    existingVote.CreatedAt = DateTime.UtcNow; // Update the timestamp
                    await _voteRepository.UpdateAsync(existingVote);

                    post.UpvoteCount--;
                    post.DownvoteCount++;
                    successMessage = "Vote changed to downvote.";
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
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to downvote post: {errorMessage}", ResponseData = false };
            }
        }
    }
}
