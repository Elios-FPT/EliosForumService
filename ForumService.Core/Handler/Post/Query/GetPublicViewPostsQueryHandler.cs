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
    public class GetPublicViewPostsQueryHandler : IQueryHandler<GetPublicViewPostsQuery, BaseResponseDto<IEnumerable<PostViewDto>>>
    {
        private readonly IPostQueryRepository _postQueryRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetPublicViewPostsQueryHandler(IPostQueryRepository postQueryRepository, IKafkaProducerRepository<User> producerRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
        }

        public async Task<BaseResponseDto<IEnumerable<PostViewDto>>> Handle(GetPublicViewPostsQuery request, CancellationToken cancellationToken)
        {
            if (request.Limit <= 0 || request.Offset < 0)
            {
 
                return new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 400,};
            }

            try
            {
             
                var postDtos = (await _postQueryRepository.GetPublicViewPostsAsync(request)).ToList();

          
                if (!postDtos.Any())
                {
                    return new BaseResponseDto<IEnumerable<PostViewDto>>
                    {
                        Status = 200,
                        Message = "No posts found.",
                        ResponseData = Enumerable.Empty<PostViewDto>()
                    };
                }

          
                try
                {
       
                    var authorIds = postDtos.Select(p => p.AuthorId).Distinct().ToList();

        
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
                catch (Exception userEx)
                {
               
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
                return new BaseResponseDto<IEnumerable<PostViewDto>> { Status = 500,  };
            }
        }
    }
}

