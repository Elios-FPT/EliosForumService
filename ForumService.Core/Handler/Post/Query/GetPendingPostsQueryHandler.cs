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
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetPendingPostsQueryHandler(IPostQueryRepository postQueryRepository, IKafkaProducerRepository<User> producerRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
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

                var postDtos = (await _postQueryRepository.GetPendingPostsAsync(request)).ToList();

                if (!postDtos.Any())
                {
                    return new BaseResponseDto<IEnumerable<PostViewDto>>
                    {
                        Status = 200,
                        Message = "No pending posts found.",
                        ResponseData = postDtos
                    };
                }


                try
                {
                    var authorIds = postDtos.Select(p => p.AuthorId).Distinct().ToList();
                    if (authorIds.Any())
                    {

                        var userProfilesList = await _producerRepository.ProduceGetAllAsync(
                            DestinationService,
                            ResponseTopic);

                        var userProfilesDict = userProfilesList.ToDictionary(u => u.id);


                        foreach (var post in postDtos)
                        {
                            if (userProfilesDict.TryGetValue(post.AuthorId, out var author))
                            {
                                post.AuthorFirstName = author.firstName;
                                post.AuthorLastName = author.lastName;
                                post.AuthorAvatarUrl = author.avatarUrl;
                            }
                        }
                    }
                }
                catch (Exception userEx)
                {

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

