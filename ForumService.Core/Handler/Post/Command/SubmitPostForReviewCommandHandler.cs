using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models; // <-- Added using for Tag and PostTag
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class SubmitPostForReviewCommandHandler : ICommandHandler<SubmitPostForReviewCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Tag> _tagRepository;
        private readonly IGenericRepository<PostTag> _postTagRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SubmitPostForReviewCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Tag> tagRepository,
            IGenericRepository<PostTag> postTagRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _tagRepository = tagRepository;
            _postTagRepository = postTagRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(SubmitPostForReviewCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var post = await _postRepository.GetByIdAsync(request.PostId);

                // --- Initial validation steps ---
                if (post == null || post.IsDeleted)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };
                }

                if (post.AuthorId != request.RequesterId)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to submit this post.", ResponseData = false };
                }

                if (post.Status != "Draft")
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 400, Message = $"Only posts with 'Draft' status can be submitted. Current status is '{post.Status}'.", ResponseData = false };
                }

                // --- Handle Tags ---
                // 1. Remove all old PostTag records of this post to refresh them
                var existingPostTags = await _postTagRepository.GetListAsync(pt => pt.PostId == post.PostId);
                if (existingPostTags.Any())
                {
                    await _postTagRepository.DeleteRangeAsync(existingPostTags);
                }

                // 2. Process the new tag list
                if (request.Tags != null && request.Tags.Any())
                {
                    var newPostTags = new List<PostTag>();
                    // Get distinct tag names, trim whitespace, and convert to lowercase
                    var distinctTagNames = request.Tags.Select(t => t.Trim().ToLower()).Where(t => !string.IsNullOrEmpty(t)).Distinct();

                    foreach (var tagName in distinctTagNames)
                    {
                        var existingTag = await _tagRepository.GetOneAsync(filter: t => t.Name == tagName);

                        Tag tagToAssociate;

                        if (existingTag == null)
                        {
                            // If the tag doesn't exist -> create a new one
                            tagToAssociate = new Tag
                            {
                                TagId = Guid.NewGuid(),
                                Name = tagName,
                                Slug = GenerateSlug(tagName), // Generate slug from name
                                CreatedAt = DateTime.UtcNow
                            };
                            await _tagRepository.AddAsync(tagToAssociate);
                        }
                        else
                        {
                            // If the tag exists -> use it
                            tagToAssociate = existingTag;
                        }

                        // Create PostTag association
                        newPostTags.Add(new PostTag { PostId = post.PostId, TagId = tagToAssociate.TagId });
                    }
                    // Add all new associations to the DB
                    await _postTagRepository.AddRangeAsync(newPostTags);
                }

                // --- Update post status ---
                post.Status = "PendingReview";
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.RequesterId;

                await _postRepository.UpdateAsync(post);
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = "Post submitted for review successfully with tags.", ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to submit post: {errorMessage}", ResponseData = false };
            }
        }

        private static string GenerateSlug(string phrase)
        {
            string str = phrase.ToLower().Trim();
            str = Regex.Replace(str, @"[^a-z0-9\s-]", ""); // Remove invalid characters
            str = Regex.Replace(str, @"\s+", "-").Trim(); // Convert spaces to hyphens
            str = str.Substring(0, str.Length <= 45 ? str.Length : 45).Trim(); // Trim length
            str = Regex.Replace(str, @"-+", "-"); // Replace multiple hyphens with a single one
            return str;
        }
    }
}
