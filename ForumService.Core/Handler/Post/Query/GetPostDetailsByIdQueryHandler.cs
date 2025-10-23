using ForumService.Contract.Message;
using ForumService.Contract.Models;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Comment;
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
    public class GetPostDetailsByIdQueryHandler : IQueryHandler<GetPostDetailsByIdQuery, BaseResponseDto<PostViewDetailDto>>
    {
        private readonly IPostQueryRepository _postQueryRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetPostDetailsByIdQueryHandler(IPostQueryRepository postQueryRepository, IKafkaProducerRepository<User> producerRepository)
        {
            _postQueryRepository = postQueryRepository ?? throw new ArgumentNullException(nameof(postQueryRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
        }

        public async Task<BaseResponseDto<PostViewDetailDto>> Handle(GetPostDetailsByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
            
                var (postDetail, flatComments) = await _postQueryRepository.GetPostDetailsByIdAsync(request.PostId);

                if (postDetail == null)
                {
                    return new BaseResponseDto<PostViewDetailDto>
                    {
                        Status = 404,
                        Message = $"Post with ID {request.PostId} not found or is not published.",
                        ResponseData = null
                    };
                }

                var allComments = flatComments.ToList();

          
                try
                {
                 
                    var authorIds = new HashSet<Guid> { postDetail.AuthorId };
                    foreach (var comment in allComments)
                    {
                        authorIds.Add(comment.AuthorId);
                    }

                    if (authorIds.Any())
                    {
                      
                        var userProfilesList = await _producerRepository.ProduceGetAllAsync(
                           DestinationService,
                           ResponseTopic);

                        var userProfilesDict = userProfilesList.ToDictionary(u => u.id);

                     
                        if (userProfilesDict.TryGetValue(postDetail.AuthorId, out var postAuthor))
                        {
                            postDetail.AuthorFirstName = postAuthor.firstName;
                            postDetail.AuthorLastName = postAuthor.lastName;
                            postDetail.AuthorAvatarUrl = postAuthor.avatarUrl;
                        }

                 
                        foreach (var comment in allComments)
                        {
                            if (userProfilesDict.TryGetValue(comment.AuthorId, out var commentAuthor))
                            {
                                comment.AuthorFirstName = commentAuthor.firstName;
                                comment.AuthorLastName = commentAuthor.lastName;
                                comment.AuthorAvatarUrl = commentAuthor.avatarUrl;
                            }
                        }
                    }
                }
                catch (Exception userEx)
                {
                  
                }

              
                postDetail.Comments = BuildCommentTree(allComments);

               
                return new BaseResponseDto<PostViewDetailDto>
                {
                    Status = 200,
                    Message = "Post details retrieved successfully.",
                    ResponseData = postDetail
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<PostViewDetailDto>
                {
                    Status = 500,
                    Message = $"An error occurred: {ex.Message}",
                    ResponseData = null
                };
            }
        }


        private List<CommentDto> BuildCommentTree(List<CommentDto> flatComments)
        {
            var commentLookup = flatComments.ToDictionary(c => c.CommentId);
            var nestedComments = new List<CommentDto>();

            foreach (var comment in flatComments)
            {
                if (comment.ParentCommentId.HasValue && commentLookup.TryGetValue(comment.ParentCommentId.Value, out var parentComment))
                {
                   
                    parentComment.Replies.Add(comment);
                }
                else
                {
                   
                    nestedComments.Add(comment);
                }
            }

            return nestedComments;
        }
    }
}

