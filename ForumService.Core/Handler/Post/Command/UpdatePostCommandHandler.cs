using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions; 
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class UpdatePostCommandHandler : ICommandHandler<UpdatePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.BannedKeyword> _bannedKeywordRepository;
        private readonly IGenericRepository<Domain.Models.Tag> _tagRepository;
        private readonly IGenericRepository<Domain.Models.PostTag> _postTagRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.BannedKeyword> bannedKeywordRepository,
            IGenericRepository<Domain.Models.Tag> tagRepository,
            IGenericRepository<Domain.Models.PostTag> postTagRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository;
            _bannedKeywordRepository = bannedKeywordRepository ?? throw new ArgumentNullException(nameof(bannedKeywordRepository));
            _tagRepository = tagRepository; 
            _postTagRepository = postTagRepository; 
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
        {
            // 1️ Validate input
            if (request.PostId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "PostId, Title, and Content cannot be empty.", ResponseData = false };
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var post = await _postRepository.GetByIdAsync(request.PostId);
                if (post == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = $"Post with ID {request.PostId} not found.", ResponseData = false };
                }

                if (post.AuthorId != request.RequesterId)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to update this post.", ResponseData = false };
                }

                string newStatus = "Draft"; 
                bool shouldCheckKeywords = false;

                if (post.PostType == "Post")
                {
                    if (request.SubmitForReview)
                    {
                        newStatus = "PendingReview";
                        shouldCheckKeywords = true;
                    }
                    else
                    {
                        newStatus = "Draft";
                        shouldCheckKeywords = false;
                    }
                }
                else if (post.PostType == "Solution" || post.PostType == "Project")
                {
                    newStatus = "Published";
                    shouldCheckKeywords = true;
                }

                if (shouldCheckKeywords)
                {
                    if (await ContainsBannedKeywordAsync(request.Title))
                    {
                        await _unitOfWork.RollbackAsync();
                        return new BaseResponseDto<bool> { Status = 400, Message = "Tiêu đề bài viết chứa từ khóa không phù hợp.", ResponseData = false };
                    }

                    if (await ContainsBannedKeywordAsync(request.Content))
                    {
                        await _unitOfWork.RollbackAsync();
                        return new BaseResponseDto<bool> { Status = 400, Message = "Nội dung bài viết chứa từ khóa không phù hợp.", ResponseData = false };
                    }
                }

                var oldPostTags = await _postTagRepository.GetListAsync(filter: pt => pt.PostId == post.PostId);
                if (oldPostTags.Any())
                {
                    await _postTagRepository.DeleteRangeAsync(oldPostTags);
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
                    await _postTagRepository.AddRangeAsync(postTagsToAdd);
                }

                // Update post information
                post.Title = request.Title;
                post.Content = request.Content;
                post.CategoryId = request.CategoryId;
                post.Status = newStatus;
                post.ReferenceId = request.ReferenceId;
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.RequesterId;

                await _postRepository.UpdateAsync(post);

                // Commit transaction
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = "Post updated successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to update post: {ex.Message}", ResponseData = false };
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

        private async Task<bool> ContainsBannedKeywordAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

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
                            return true;
                        }
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }
            }
            return false;
        }

    }
}