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
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        // THAY ĐỔI: Thêm 2 repo cho Tags
        private readonly IGenericRepository<Domain.Models.Tag> _tagRepository;
        private readonly IGenericRepository<Domain.Models.PostTag> _postTagRepository;
        private readonly IUnitOfWork _unitOfWork;
        // THAY ĐỔI: Bỏ utility service
        // private readonly ISUtilityServiceClient _utilityServiceClient;

        public UpdatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            // Thêm 2 repo
            IGenericRepository<Domain.Models.Tag> tagRepository,
            IGenericRepository<Domain.Models.PostTag> postTagRepository,
            IUnitOfWork unitOfWork)
        // Bỏ ISUtilityServiceClient
        {
            _postRepository = postRepository;
            _attachmentRepository = attachmentRepository;
            _tagRepository = tagRepository; // Thêm
            _postTagRepository = postTagRepository; // Thêm
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

                // 6️ Update post information
                post.Title = request.Title;
                post.Summary = request.Summary;
                post.Content = request.Content;
                post.CategoryId = request.CategoryId;
                post.Status = "Draft"; 
                post.UpdatedAt = DateTime.UtcNow;
                post.UpdatedBy = request.RequesterId;

                await _postRepository.UpdateAsync(post);

                // 7️ Commit transaction
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
    }
}