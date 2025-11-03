using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    /// <summary>
    /// Handles creating a post, uploading files, processing tags, 
    /// and setting the status to 'PendingReview' in a single transaction.
    /// </summary>
    public class CreateAndSubmitPostCommandHandler : ICommandHandler<CreateAndSubmitPostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        private readonly IGenericRepository<Domain.Models.Tag> _tagRepository;
        private readonly IGenericRepository<Domain.Models.PostTag> _postTagRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient;

        public CreateAndSubmitPostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            IGenericRepository<Domain.Models.Tag> tagRepository,
            IGenericRepository<Domain.Models.PostTag> postTagRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
            _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
            _postTagRepository = postTagRepository ?? throw new ArgumentNullException(nameof(postTagRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
        }

        public async Task<BaseResponseDto<bool>> Handle(CreateAndSubmitPostCommand request, CancellationToken cancellationToken)
        {
            // 1️ Validate input
            if (request.AuthorId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "AuthorId, Title, and Content cannot be empty.", ResponseData = false };
            }


            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 2️ Create Post entity
                var post = new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    AuthorId = request.AuthorId,
                    CategoryId = request.CategoryId,
                    Title = request.Title,
                    Content = request.Content,
                    PostType = request.PostType ?? "Post",
                    Status = "PendingReview",
                    IsDeleted = false,
                    IsFeatured = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.AuthorId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.AuthorId
                };

                await _postRepository.AddAsync(post);

                if (request.FilesToUpload is not null && request.FilesToUpload.Any())
                {
                    var attachments = new List<Domain.Models.Attachment>();
                    foreach (var file in request.FilesToUpload)
                    {
                        var keyPrefix = $"posts/{post.PostId}";
                        var uploadedUrl = await _utilityServiceClient.UploadFileAsync(keyPrefix, file, cancellationToken);

                        if (string.IsNullOrEmpty(uploadedUrl))
                        {
                            await _unitOfWork.RollbackAsync();
                            return new BaseResponseDto<bool> { Status = 400, Message = $"Failed to upload file: {file.FileName}. Post creation cancelled.", ResponseData = false };
                        }

                        attachments.Add(new Domain.Models.Attachment
                        {
                            AttachmentId = Guid.NewGuid(),
                            TargetType = "Post",
                            TargetId = post.PostId,
                            Filename = file.FileName,
                            Url = uploadedUrl,
                            ContentType = file.ContentType,
                            SizeBytes = file.Content.Length,
                            UploadedBy = request.AuthorId,
                            UploadedAt = DateTime.UtcNow
                        });
                    }
                    await _attachmentRepository.AddRangeAsync(attachments);
                }

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

                return new BaseResponseDto<bool> { Status = 200, Message = "Post created and submitted for review successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to create and submit post: {errorMessage}", ResponseData = false };
            }
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
    }
}

