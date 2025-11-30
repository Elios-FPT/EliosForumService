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
    public class GetModeratorPublicPostsQueryHandler : IQueryHandler<GetModeratorPublicPostsQuery, PagedResponseDto<IEnumerable<ModeratorPostViewDto>>>
    {
        private readonly IPostQueryRepository _postQueryRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetModeratorPublicPostsQueryHandler(IPostQueryRepository postQueryRepository, IKafkaProducerRepository<User> producerRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
        }

        public async Task<PagedResponseDto<IEnumerable<ModeratorPostViewDto>>> Handle(GetModeratorPublicPostsQuery request, CancellationToken cancellationToken)
        {
            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.Size <= 0 ? 10 : request.Size;

            try
            {
                var result = await _postQueryRepository.GetModeratorPublicViewPostsAsync(request);
                var posts = result.Posts.ToList();
                var totalItems = result.TotalCount;

                if (!posts.Any())
                {
                    return new PagedResponseDto<IEnumerable<ModeratorPostViewDto>>(Enumerable.Empty<ModeratorPostViewDto>(), page, pageSize, 0)
                    { Message = "No published posts found." };
                }

                Dictionary<Guid, User> userProfilesDict;
                try
                {
                    var userProfilesList = await _producerRepository.ProduceGetAllAsync(DestinationService, ResponseTopic);
                    userProfilesDict = userProfilesList.ToDictionary(u => u.id);
                }
                catch { userProfilesDict = new Dictionary<Guid, User>(); }

                var postDtos = MapPostsToDtos(posts, userProfilesDict);

                return new PagedResponseDto<IEnumerable<ModeratorPostViewDto>>(postDtos, page, pageSize, totalItems)
                { Message = "Moderator public posts retrieved successfully." };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to retrieve moderator posts: {ex}");
                return new PagedResponseDto<IEnumerable<ModeratorPostViewDto>> { Status = 500, Message = $"An error occurred: {ex.Message}" };
            }
        }

        private List<ModeratorPostViewDto> MapPostsToDtos(List<Domain.Models.Post> posts, Dictionary<Guid, User> userProfilesDict)
        {
            var postDtos = new List<ModeratorPostViewDto>();
            foreach (var post in posts)
            {
                userProfilesDict.TryGetValue(post.AuthorId, out var authorProfile);
                userProfilesDict.TryGetValue(post.ModeratedBy ?? Guid.Empty, out var moderatorProfile);
                userProfilesDict.TryGetValue(post.DeletedBy ?? Guid.Empty, out var deleterProfile);

                postDtos.Add(new ModeratorPostViewDto
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
                    CategoryName = post.Category?.Name,
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
                    ReferenceId = post.ReferenceId
                });
            }
            return postDtos;
        }
    }
}
