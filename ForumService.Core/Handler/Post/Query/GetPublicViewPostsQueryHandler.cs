using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Post;
using ForumService.Contract.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Core.Handler.Post.Query
{

    public class GetPublicViewPostsQueryHandler : IQueryHandler<GetPublicViewPostsQuery, PagedResponseDto<IEnumerable<PostViewDto>>>
    {
        private readonly IPostQueryRepository _postQueryRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetPublicViewPostsQueryHandler(IPostQueryRepository postQueryRepository,
            IKafkaProducerRepository<User> producerRepository,
            IGenericRepository<Domain.Models.Category> categoryRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        }

        public async Task<PagedResponseDto<IEnumerable<PostViewDto>>> Handle(GetPublicViewPostsQuery request, CancellationToken cancellationToken)
        {
            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.Size <= 0 ? 10 : request.Size;

            try
            {

                var result = await _postQueryRepository.GetPublicViewPostsAsync(request);
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


                // 1. Get all unique AuthorIds from the posts
                var authorIds = posts.Select(p => p.AuthorId).Distinct().ToList();

                // 2. Fetch User Profiles
                Dictionary<Guid, User> userProfilesDict;
                try
                {
                    var userProfilesList = await _producerRepository.ProduceGetAllAsync(
                           DestinationService,
                           ResponseTopic);


                    userProfilesDict = userProfilesList
                        .Where(u => authorIds.Contains(u.id))
                        .ToDictionary(u => u.id);
                }
                catch
                {
                    userProfilesDict = new Dictionary<Guid, User>();
                }


                var postDtos = new List<PostViewDto>();
                foreach (var post in posts)
                {
                    userProfilesDict.TryGetValue(post.AuthorId, out var authorProfile);

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
                        AuthorFirstName = authorProfile?.firstName,
                        AuthorLastName = authorProfile?.lastName,
                        AuthorAvatarUrl = authorProfile?.avatarUrl,
                        CategoryName = post.Category?.Name,
                        ReferenceId = post.ReferenceId
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
                    Message = $"An error occurred: {ex.Message}",
                    ResponseData = null,
                    Pagination = null
                };
            }
        }
    }
}