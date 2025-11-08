using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.UseCases.Post;
using ForumService.Core.Interfaces;
using Domain = ForumService.Domain.Models; // Alias for brevity
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
    /// Unified handler for creating posts (Draft OR PendingReview).
    /// Handles basic info, attachments linking, and tags processing.
    /// </summary>
    public class CreatePostCommandHandler : ICommandHandler<CreatePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Tag> _tagRepository;
        private readonly IGenericRepository<Domain.Models.PostTag> _postTagRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            IGenericRepository<Domain.Models.Tag> tagRepository,
            IGenericRepository<Domain.Models.PostTag> postTagRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
            _postTagRepository = postTagRepository ?? throw new ArgumentNullException(nameof(postTagRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<bool>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
        {
            // 1️⃣ Validate basic input
            if (request.AuthorId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "AuthorId, Title, and Content cannot be empty.", ResponseData = false };
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 2️⃣ Determine initial status based on the flag
                string initialStatus = request.SubmitForReview ? "PendingReview" : "Draft";

                // 3️⃣ Create Post entity
                var post = new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    AuthorId = request.AuthorId,
                    CategoryId = request.CategoryId,
                    Title = request.Title,
                    Content = request.Content,
                    PostType = request.PostType ?? "Post",
                    ReferenceId = request.ReferenceId,
                    Status = initialStatus, // Set status here
                    IsDeleted = false,
                    IsFeatured = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.AuthorId,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.AuthorId
                };

                await _postRepository.AddAsync(post);


                // 5️⃣ Handle Tags processing (if any)
                if (request.Tags != null && request.Tags.Any())
                {
                    var postTagsToAdd = new List<Domain.Models.PostTag>();
                    var uniqueTagNames = request.Tags
                        .Select(t => t.ToLowerInvariant().Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .Distinct();

                    foreach (var tagName in uniqueTagNames)
                    {
                        // Find existing tag or create new one
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

                        // Link tag to post
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

                var successMessage = request.SubmitForReview
                    ? "Post created and submitted for review successfully."
                    : "Post draft saved successfully.";

                return new BaseResponseDto<bool> { Status = 200, Message = post.PostId.ToString(), ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to create post: {errorMessage}", ResponseData = false };
            }
        }

        // Helper to generate slug for new tags
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