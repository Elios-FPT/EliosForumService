using ForumService.Contract.Message;
using ForumService.Contract.Models;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Contract.TransferObjects.User;
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
    public class GetPendingPostsQueryHandler : IQueryHandler<GetPendingPostsQuery, BaseResponseDto<IEnumerable<PostViewDto>>>
    {
        private readonly IPostQueryRepository _postQueryRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetPendingPostsQueryHandler(
            IPostQueryRepository postQueryRepository,
            IKafkaProducerRepository<User> producerRepository,
            IGenericRepository<Domain.Models.Category> categoryRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        }

        public async Task<BaseResponseDto<IEnumerable<PostViewDto>>> Handle(GetPendingPostsQuery request, CancellationToken cancellationToken)
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
                var posts = (await _postQueryRepository.GetPendingPostsAsync(request)).ToList();

                if (!posts.Any())
                {
                    return new BaseResponseDto<IEnumerable<PostViewDto>>
                    {
                        Status = 200,
                        Message = "No pending posts found.",
                        ResponseData = Enumerable.Empty<PostViewDto>()
                    };
                }

                Dictionary<Guid, User> userProfilesDict;

                try
                {
                    var userProfilesList = await _producerRepository.ProduceGetAllAsync(
                        DestinationService,
                        ResponseTopic);

                    userProfilesDict = userProfilesList.ToDictionary(u => u.id);
                }
                catch (Exception)
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
                        AuthorAvatarUrl = authorProfile?.avatarUrl
                    };

                    var category = await _categoryRepository.GetByIdAsync(post.CategoryId);
                    postDto.CategoryName = category?.Name;

                    postDtos.Add(postDto);
                }

                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 200,
                    Message = "Pending posts retrieved successfully.",
                    ResponseData = postDtos
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<PostViewDto>>
                {
                    Status = 500,
                    Message = $"An error occurred: {ex.Message}",
                    ResponseData = Enumerable.Empty<PostViewDto>()
                };
            }
        }
    }
}
