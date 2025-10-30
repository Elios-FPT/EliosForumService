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
    public class GetModeratorPublicPostsQueryHandler
        : IQueryHandler<GetModeratorPublicPostsQuery, BaseResponseDto<IEnumerable<ModeratorPostViewDto>>>
    {
        private readonly IPostQueryRepository _postQueryRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetModeratorPublicPostsQueryHandler(
            IPostQueryRepository postQueryRepository,
            IKafkaProducerRepository<User> producerRepository,
            IGenericRepository<Domain.Models.Category> categoryRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
        }

        public async Task<BaseResponseDto<IEnumerable<ModeratorPostViewDto>>> Handle(
            GetModeratorPublicPostsQuery request,
            CancellationToken cancellationToken)
        {
            if (request.Limit <= 0 || request.Offset < 0)
            {
                return new BaseResponseDto<IEnumerable<ModeratorPostViewDto>>
                {
                    Status = 400,
                    Message = "Limit must be positive and Offset must be non-negative.",
                    ResponseData = Enumerable.Empty<ModeratorPostViewDto>()
                };
            }

            try
            {
 
                var posts = (await _postQueryRepository.GetModeratorPublicViewPostsAsync(request)).ToList();

                if (!posts.Any())
                {
                    return new BaseResponseDto<IEnumerable<ModeratorPostViewDto>>
                    {
                        Status = 200,
                        Message = "No published posts found.",
                        ResponseData = Enumerable.Empty<ModeratorPostViewDto>()
                    };
                }


                Dictionary<Guid, User> userProfilesDict;
                try
                {
                    var userProfilesList = await _producerRepository.ProduceGetAllAsync(DestinationService, ResponseTopic);
                    userProfilesDict = userProfilesList.ToDictionary(u => u.id);
                }
                catch
                {
                    userProfilesDict = new Dictionary<Guid, User>();
                }

                var postDtos = new List<ModeratorPostViewDto>();
                foreach (var post in posts)
                {
                    userProfilesDict.TryGetValue(post.AuthorId, out var authorProfile);
                    userProfilesDict.TryGetValue(post.ModeratedBy ?? Guid.Empty, out var moderatorProfile);
                    userProfilesDict.TryGetValue(post.DeletedBy ?? Guid.Empty, out var deleterProfile);

                    
                    string? categoryName = null;
                    if (post.CategoryId.HasValue)
                    {
                        var category = await _categoryRepository.GetByIdAsync(post.CategoryId.Value);
                        categoryName = category?.Name;
                    }

                

                    var postDto = new ModeratorPostViewDto
                    {
                        PostId = post.PostId,
                        AuthorId = post.AuthorId,
                        CategoryId = post.CategoryId,
                        Title = post.Title,
                        Summary = post.Summary,
                        Content = post.Content,
                        PostType = post.PostType,
                        Status = post.Status,
                        ViewsCount = post.ViewsCount,
                        CommentCount = post.CommentCount,
                        UpvoteCount = post.UpvoteCount,
                        DownvoteCount = post.DownvoteCount,
                        IsFeatured = post.IsFeatured,
                        IsDeleted = post.IsDeleted,
                        CreatedAt = post.CreatedAt,
                        UpdatedAt = post.UpdatedAt,

                        CategoryName = categoryName,

                        AuthorFirstName = authorProfile?.firstName,
                        AuthorLastName = authorProfile?.lastName,
                        AuthorAvatarUrl = authorProfile?.avatarUrl,

                        ModeratedBy = post.ModeratedBy,
                        ModeratedAt = post.ModeratedAt,
                        RejectionReason = post.RejectionReason,
                        ModeratorFirstName = moderatorProfile?.firstName,
                        ModeratorLastName = moderatorProfile?.lastName,
                        ModeratorAvatarUrl = moderatorProfile?.avatarUrl,

                        DeletedBy = post.DeletedBy,
                        DeletedAt = post.DeletedAt,
                        DeletedByFirstName = deleterProfile?.firstName,
                        DeletedByLastName = deleterProfile?.lastName,
                        DeletedByAvatarUrl = deleterProfile?.avatarUrl,
                    };

                    postDtos.Add(postDto);
                }

                return new BaseResponseDto<IEnumerable<ModeratorPostViewDto>>
                {
                    Status = 200,
                    Message = "Moderator public posts retrieved successfully.",
                    ResponseData = postDtos
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to retrieve moderator posts: {ex}");
                return new BaseResponseDto<IEnumerable<ModeratorPostViewDto>>
                {
                    Status = 500,
                    Message = $"An error occurred: {ex.Message}",
                    ResponseData = Enumerable.Empty<ModeratorPostViewDto>()
                };
            }
        }
    }
}
