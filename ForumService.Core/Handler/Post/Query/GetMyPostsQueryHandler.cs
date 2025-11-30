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
    public class GetMyPostsQueryHandler : IQueryHandler<GetMyPostsQuery, PagedResponseDto<IEnumerable<PostViewDto>>>
    {
        private readonly IPostQueryRepository _postQueryRepository;

        public GetMyPostsQueryHandler(IPostQueryRepository postQueryRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
        }

        public async Task<PagedResponseDto<IEnumerable<PostViewDto>>> Handle(GetMyPostsQuery request, CancellationToken cancellationToken)
        {
            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.Size <= 0 ? 10 : request.Size;

            try
            {
                var result = await _postQueryRepository.GetMyPostsAsync(request);
                var posts = result.Posts.ToList();
                var totalItems = result.TotalCount;

                if (!posts.Any())
                {
                    return new PagedResponseDto<IEnumerable<PostViewDto>>(
                        Enumerable.Empty<PostViewDto>(),
                        page,
                        pageSize,
                        0
                    )
                    {
                        Message = "No posts found."
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
                        CreatedAt = post.CreatedAt,
                        ReferenceId = post.ReferenceId,
                        CategoryName = post.Category?.Name
                    };

                    postDtos.Add(postDto);
                }

                return new PagedResponseDto<IEnumerable<PostViewDto>>(
                    postDtos,
                    page,
                    pageSize,
                    totalItems
                )
                {
                    Message = "Posts retrieved successfully."
                };
            }
            catch (Exception ex)
            {
                return new PagedResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 500,
                    Message = $"An internal server error occurred: {ex.Message}",
                    ResponseData = null,
                    Pagination = null
                };
            }
        }
    }
}
