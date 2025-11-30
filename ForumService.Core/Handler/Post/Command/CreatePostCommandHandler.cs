using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Contract.UseCases.Post;
using ForumService.Core.Interfaces;
using ForumService.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    /// <summary>
    /// Unified handler for creating posts (Draft OR PendingReview).
    /// Handles basic info, attachments linking, and tags processing.
    /// </summary>
    public class CreatePostCommandHandler : ICommandHandler<CreatePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Tag> _tagRepository;
        private readonly IGenericRepository<Domain.Models.PostTag> _postTagRepository;
        private readonly IGenericRepository<Domain.Models.BannedKeyword> _bannedKeywordRepository;
        private readonly IUnitOfWork _unitOfWork;

        private readonly IKafkaProducer _kafkaProducer;
        private readonly ILogger<CreatePostCommandHandler> _logger;
        private readonly string _topicName;

        public CreatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Tag> tagRepository,
            IGenericRepository<Domain.Models.PostTag> postTagRepository,
            IGenericRepository<Domain.Models.BannedKeyword> bannedKeywordRepository,
            IUnitOfWork unitOfWork,

            IKafkaProducer kafkaProducer,
            IAppConfiguration appConfig,  
            ILogger<CreatePostCommandHandler> logger
            )

        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
            _postTagRepository = postTagRepository ?? throw new ArgumentNullException(nameof(postTagRepository));
            _bannedKeywordRepository = bannedKeywordRepository ?? throw new ArgumentNullException(nameof(bannedKeywordRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

            _kafkaProducer = kafkaProducer ?? throw new ArgumentNullException(nameof(kafkaProducer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Standard Topic name configuration: forum-user-userstats
            string currentServiceName = appConfig.GetCurrentServiceName();
            _topicName = $"{currentServiceName}-user-userstats";
        }

        public async Task<BaseResponseDto<bool>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
        {
            if (request.AuthorId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "AuthorId, Title, and Content cannot be empty.", ResponseData = false };
            }

            string initialStatus;
            string currentPostType = request.PostType ?? "Post";

            // Solution/Project then Published 
            if (currentPostType == "Solution" || currentPostType == "Project")
            {
                initialStatus = "Published";
            }
            else
            {
                initialStatus = request.SubmitForReview ? "PendingReview" : "Draft";
            }

            // Check ban keyword 
            if (initialStatus == "Published" || initialStatus == "PendingReview")
            {
                var bannedKeywordInTitle = await GetBannedKeywordAsync(request.Title);
                if (!string.IsNullOrEmpty(bannedKeywordInTitle))
                {
                    return new BaseResponseDto<bool>
                    {
                        Status = 400,
                        Message = $"The post title contains a banned keyword: '{bannedKeywordInTitle}'",
                        ResponseData = false
                    };
                }

                var bannedKeywordInContent = await GetBannedKeywordAsync(request.Content);
                if (!string.IsNullOrEmpty(bannedKeywordInContent))
                {
                    return new BaseResponseDto<bool>
                    {
                        Status = 400,
                        Message = $"The post content contains a banned keyword: '{bannedKeywordInContent}'",
                        ResponseData = false
                    };
                }
            }

            await _unitOfWork.BeginTransactionAsync();
            Domain.Models.Post post; 

            try
            {
                post = new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    AuthorId = request.AuthorId,
                    CategoryId = request.CategoryId,
                    Title = request.Title,
                    Content = request.Content,
                    PostType = currentPostType,
                    ReferenceId = request.ReferenceId,
                    Status = initialStatus,
                    IsDeleted = false,
                    IsFeatured = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.AuthorId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.AuthorId
                };

                await _postRepository.AddAsync(post);

                // Processing Tags 
                if (request.Tags != null && request.Tags.Any())
                {
                    var postTagsToAdd = new List<Domain.Models.PostTag>();
                    var uniqueTagNames = request.Tags
                        .Select(t => t.ToLowerInvariant().Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct();

                    foreach (var tagName in uniqueTagNames)
                    {
                        var tagEntity = await _tagRepository.GetOneAsync(t => t.Name == tagName);
                        if (tagEntity == null)
                        {
                            tagEntity = new Domain.Models.Tag
                            {
                                TagId = Guid.NewGuid(),
                                Name = tagName,
                                Slug = GenerateSlug(tagName),
                                CreatedAt = DateTime.UtcNow
                            };
                            await _tagRepository.AddAsync(tagEntity);
                        }

                        postTagsToAdd.Add(new Domain.Models.PostTag
                        {
                            PostId = post.PostId,
                            TagId = tagEntity.TagId
                        });
                    }

                    if (postTagsToAdd.Any())
                    {
                        await _postTagRepository.AddRangeAsync(postTagsToAdd);
                    }
                }

                await _unitOfWork.CommitAsync();
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to create post: {errorMessage}", ResponseData = false };
            }

            // LOGIC KAFKA: Only send if the status is published immediately
            if (post.Status == "Published")
            {
                try
                {
                    var eventPayload = new PostApprovedEvent
                    {
                        PostId = post.PostId,
                        UserId = post.AuthorId,
                        ApprovedAt = DateTime.UtcNow
                    };

                    // EventWrapper
                    var wrapper = new EventWrapper(
                        EventType: "POST_APPROVED", 
                        ModelType: nameof(PostApprovedEvent),
                        Payload: eventPayload,
                        EventId: Guid.NewGuid().ToString(),
                        CorrelationId: Guid.NewGuid().ToString(),
                        Timestamp: DateTime.UtcNow
                    );

                    string jsonPayload = JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    // Send
                    await _kafkaProducer.ProduceAsync(_topicName, post.AuthorId.ToString(), jsonPayload, cancellationToken);

                    _logger.LogInformation("Sent stats event for auto-published post {PostId}", post.PostId);
                }
                catch (Exception kafkaEx)
                {
                    _logger.LogError(kafkaEx, "Post {PostId} created but failed to send stats event.", post.PostId);
                }
            }
            // END KAFKA LOGIC

            var successMessage = request.SubmitForReview
                ? "Post created and submitted for review successfully."
                : "Post draft saved successfully.";

            return new BaseResponseDto<bool> { Status = 200, Message = post.PostId.ToString(), ResponseData = true };
        }


        private static string GenerateSlug(string phrase)
        {
            string str = phrase.ToLowerInvariant().Trim();
            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            str = Regex.Replace(str, @"\s+", "-").Trim();
            str = str[..(str.Length <= 45 ? str.Length : 45)];
            str = Regex.Replace(str, @"-+", "-");
            return str;
        }

        private async Task<string?> GetBannedKeywordAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;

            var bannedKeywords = await _bannedKeywordRepository.GetListAsync(x => x.IsActive);

            if (bannedKeywords != null && bannedKeywords.Any())
            {
                foreach (var banned in bannedKeywords)
                {
                    try
                    {
                        var pattern = banned.Keyword;

                        if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase))
                        {
                            return pattern; 
                        }
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }
            }
            return null; 
        }
    }
}