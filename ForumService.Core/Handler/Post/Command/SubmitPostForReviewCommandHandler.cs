using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models; 
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
        private readonly IGenericRepository<Domain.Models.Tag> _tagRepository; 
        private readonly IGenericRepository<Domain.Models.PostTag> _postTagRepository; 
        private readonly IUnitOfWork _unitOfWork;

        public SubmitPostForReviewCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Tag> tagRepository, 
            IGenericRepository<Domain.Models.PostTag> postTagRepository, 
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _tagRepository = tagRepository ?? throw new ArgumentNullException(nameof(tagRepository));
            _postTagRepository = postTagRepository ?? throw new ArgumentNullException(nameof(postTagRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
                    return new BaseResponseDto<bool> { Status = 400, Message = $"Post is not in Draft status (current status: {post.Status}).", ResponseData = false };
                }

                // --- Handle Tags ---
                // 1. Remove all old PostTag records of this post
                var existingPostTags = await _postTagRepository.GetListAsync(pt => pt.PostId == post.PostId);
                if (existingPostTags.Any())
                {
                    await _postTagRepository.DeleteRangeAsync(existingPostTags);
                }

                // 2. Process the new tag list (only if not null and not empty)
                if (request.Tags != null && request.Tags.Any())
                {
                    var uniqueTagNames = request.Tags
                                             .Select(t => t.Trim().ToLower())
                                             .Where(t => !string.IsNullOrEmpty(t))
                                             .Distinct()
                                             .ToList();

                    var newPostTags = new List<PostTag>();

                    if (uniqueTagNames.Any())
                    {
                        // Find existing tags efficiently using a dictionary
                        var existingTagsDict = (await _tagRepository.GetListAsync(t => uniqueTagNames.Contains(t.Name.ToLower())))
                                               .ToDictionary(t => t.Name.ToLower(), t => t);

                        foreach (var tagName in uniqueTagNames)
                        {
                            Tag tagEntity;
                            if (existingTagsDict.TryGetValue(tagName, out var foundTag))
                            {
                                // Use existing tag
                                tagEntity = foundTag;
                            }
                            else
                            {
                                // Create new tag if it doesn't exist
                                tagEntity = new Tag
                                {
                                    TagId = Guid.NewGuid(),
                                    Name = tagName, 
                                    Slug = GenerateSlug(tagName), 
                                    CreatedAt = DateTime.UtcNow
                                };
                                await _tagRepository.AddAsync(tagEntity); 
                            }

                            // Create new PostTag association
                            newPostTags.Add(new PostTag { PostId = post.PostId, TagId = tagEntity.TagId });
                        }

                        // Add all new associations to the context
                        if (newPostTags.Any())
                        {
                            await _postTagRepository.AddRangeAsync(newPostTags);
                        }
                    }
                }
                // If request.Tags is null or empty, no new tags are added (old ones were already removed)
                
                // --- Update post status ---
                post.Status = "PendingReview";
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.RequesterId;

                await _postRepository.UpdateAsync(post); 

                // Commit all changes (Post status, Tag creations, PostTag deletions/creations)
                await _unitOfWork.CommitAsync();
                return new BaseResponseDto<bool> { Status = 200, Message = "Post submitted for review successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool> { Status = 500, Message = $"An error occurred: {ex.Message}", ResponseData = false };
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

