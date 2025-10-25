using ForumService.Contract.Message;
using ForumService.Contract.Models;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Post;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Core.Handler.Post.Query
{
    /// <summary>
    /// Handles retrieving posts created by the currently authenticated user.
    /// This version follows the consistent structure used across other post query handlers.
    /// </summary>
    public class GetMyPostsQueryHandler : IQueryHandler<GetMyPostsQuery, BaseResponseDto<IEnumerable<PostViewDto>>>
    {
        private readonly IPostQueryRepository _postQueryRepository;
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;

        public GetMyPostsQueryHandler(
            IPostQueryRepository postQueryRepository,
            IGenericRepository<Domain.Models.Category> categoryRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
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
                var posts = (await _postQueryRepository.GetMyPostsAsync(request)).ToList();

                if (!posts.Any())
                {
                    return new BaseResponseDto<IEnumerable<PostViewDto>>
                    {
                        Status = 200,
                        Message = "No posts found.",
                        ResponseData = Enumerable.Empty<PostViewDto>()
                    };
                }

                var postDtos = new List<PostViewDto>();

                foreach (var post in posts)
                {
                    var postDto = new PostViewDto
                    {
                        PostId = post.PostId,
                        AuthorId = post.AuthorId,
                        CategoryId = post.CategoryId,
                        Title = post.Title,
                        Content = post.Content,
                        PostType = post.PostType,
                        Status = post.Status,
                        ViewsCount = post.ViewsCount,
                        CommentCount = post.CommentCount,
                        UpvoteCount = post.UpvoteCount,
                        DownvoteCount = post.DownvoteCount,
                        IsFeatured = post.IsFeatured,
                        CreatedAt = post.CreatedAt
                    };

                    var category = await _categoryRepository.GetByIdAsync(post.CategoryId);
                    postDto.CategoryName = category?.Name;

                    postDtos.Add(postDto);
                }

                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 200,
                    Message = "Posts retrieved successfully.",
                    ResponseData = postDtos
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 500,
                    Message = $"An internal server error occurred: {ex.Message}",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }
        }
    }
}
