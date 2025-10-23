using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Post;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Core.Handler.Post.Query
{
    /// <summary>
    /// Handles the logic for retrieving all posts belonging to the current authenticated user.
    /// This version is simplified and does not perform data enrichment for author info.
    /// </summary>
    public class GetMyPostsQueryHandler : IQueryHandler<GetMyPostsQuery, BaseResponseDto<IEnumerable<PostViewDto>>>
    {
     
        private readonly IPostQueryRepository _postQueryRepository;

        public GetMyPostsQueryHandler(IPostQueryRepository postQueryRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
        }

        public async Task<BaseResponseDto<IEnumerable<PostViewDto>>> Handle(GetMyPostsQuery request, CancellationToken cancellationToken)
        {
            if (request.Limit <= 0 || request.Offset < 0)
            {
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 400,
                    Message = "Limit must be positive and Offset must be non-negative.",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }

            try
            {
                
                var postDtos = await _postQueryRepository.GetMyPostsAsync(request);

                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 200,
                    Message = postDtos.Any() ? "Your posts were retrieved successfully." : "You have not created any posts yet.",
                    ResponseData = postDtos
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 500,
                    Message = $"An error occurred while retrieving your posts: {ex.Message}",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }
        }
    }
}

