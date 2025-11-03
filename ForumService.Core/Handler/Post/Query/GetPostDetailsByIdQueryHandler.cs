using ForumService.Contract.Message;
using ForumService.Contract.Models;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Comment;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Tag;
using ForumService.Domain.Models;
using Microsoft.Extensions.Logging;
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
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Comment> _commentRepository;
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        private readonly ITagQueryRepository _tagRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private readonly IUnitOfWork _unitOfWork; 
        private readonly ILogger<GetPostDetailsByIdQueryHandler> _logger;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetPostDetailsByIdQueryHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Comment> commentRepository,
            IGenericRepository<Domain.Models.Category> categoryRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            ITagQueryRepository tagRepository,
            IKafkaProducerRepository<User> producerRepository,
            IUnitOfWork unitOfWork, 
            ILogger<GetPostDetailsByIdQueryHandler> logger)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _commentRepository = commentRepository ?? throw new ArgumentNullException(nameof(commentRepository));
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
            _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork)); 
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BaseResponseDto<PostViewDetailDto>> Handle(GetPostDetailsByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var postEntity = await _postRepository.GetOneAsync(
                    filter: p => p.PostId == request.PostId && p.Status == "Published" && !p.IsDeleted
                );

                if (postEntity == null)
                {
                    return new BaseResponseDto<PostViewDetailDto>
                    {
                        Status = 404,
                        Message = $"Post with ID {request.PostId} not found or is not published.",
                        ResponseData = null
                    };
                }

                // Increment the view count. This is a write operation (side-effect) within a query handler.
                try
                {
                    await _unitOfWork.BeginTransactionAsync();
                    postEntity.ViewsCount++;
                    await _postRepository.UpdateAsync(postEntity);
                    await _unitOfWork.CommitAsync();
                }
                catch (Exception viewEx)
                {
                    
                    _logger.LogWarning(viewEx, "Failed to increment view count for PostId {PostId}. Continuing to retrieve post details.", request.PostId);
                    await _unitOfWork.RollbackAsync();
                }

                var tagsTask = _tagRepository.GetTagNamesByPostIdAsync(request.PostId, cancellationToken);

                var allComments = (await _commentRepository.GetListAsyncUntracked(
                    filter: c => c.PostId == request.PostId && !c.IsDeleted,
                    selector: c => new CommentDto
                    {
                        CommentId = c.CommentId,
                        AuthorId = c.AuthorId,
                        ParentCommentId = c.ParentCommentId,
                        Content = c.Content,
                        UpvoteCount = c.UpvoteCount,
                        DownvoteCount = c.DownvoteCount,
                        CreatedAt = c.CreatedAt
                    },
                    orderBy: q => q.OrderBy(c => c.CreatedAt)
                )).ToList();

                Domain.Models.Category? category = null;
                if (postEntity.CategoryId.HasValue)
                {
                    category = await _categoryRepository.GetByIdAsync(postEntity.CategoryId.Value);
                }

                var attachmentUrls = (await _attachmentRepository.GetListAsyncUntracked(
                    filter: a => a.TargetType == "Post" && a.TargetId == postEntity.PostId,
                    selector: a => a.Url
                )).ToList();

                var tags = (await tagsTask).ToList();

                var postDetail = new PostViewDetailDto
                {
                    PostId = postEntity.PostId,
                    AuthorId = postEntity.AuthorId,
                    Title = postEntity.Title,
                    Summary = postEntity.Summary,
                    Content = postEntity.Content,
                    PostType = postEntity.PostType,
                    ViewsCount = postEntity.ViewsCount,
                    CommentCount = postEntity.CommentCount,
                    UpvoteCount = postEntity.UpvoteCount,
                    DownvoteCount = postEntity.DownvoteCount,
                    IsFeatured = postEntity.IsFeatured,
                    CreatedAt = postEntity.CreatedAt,
                    CategoryName = category?.Name,
                    Url = attachmentUrls,
                    Tags = tags.Select(t => t.Name).ToList()
                };

                try
                {
                    var authorIds = new HashSet<Guid> { postDetail.AuthorId };
                    authorIds.UnionWith(allComments.Select(c => c.AuthorId));

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
                    _logger.LogWarning(userEx, "Failed to enrich post details with user information for PostId {PostId}. Returning data without author details.", request.PostId);
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
                _logger.LogError(ex, "An error occurred while retrieving details for PostId {PostId}", request.PostId);
                return new BaseResponseDto<PostViewDetailDto>
                {
                    Status = 500,
                    Message = "An internal server error occurred.",
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
