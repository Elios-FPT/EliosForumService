using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post
{
    public class CreatePostCommandHandler : ICommandHandler<CreatePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<bool>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
        {
            // 1️⃣ Validate input
            if (request.AuthorId == Guid.Empty)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "AuthorId cannot be empty.",
                    ResponseData = false
                };
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Title cannot be empty.",
                    ResponseData = false
                };
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Content cannot be empty.",
                    ResponseData = false
                };
            }

            try
            {
                // 2️⃣ Begin transaction
                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    // 3️⃣ Create Post entity
                    var post = new Domain.Models.Post
                    {
                        PostId = Guid.NewGuid(),
                        AuthorId = request.AuthorId,
                        CategoryId = request.CategoryId,
                        Title = request.Title,
                        Summary = request.Summary,
                        Content = request.Content,
                        PostType = request.PostType ?? "Post",
                        Status = request.Status ?? "Draft",
                        ViewsCount = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    // 4️⃣ Save Post
                    await _postRepository.AddAsync(post);
                    await _unitOfWork.SaveChangesAsync();

                    // 5️⃣ Save Attachments (nếu có)
                    if (request.Attachments is not null && request.Attachments.Any())
                    {
                        var attachments = request.Attachments.Select(a => new Domain.Models.Attachment
                        {
                            AttachmentId = Guid.NewGuid(),
                            TargetType = "Post",
                            TargetId = post.PostId,
                            Filename = a.Filename,
                            Url = a.Url,
                            ContentType = a.ContentType,
                            SizeBytes = a.SizeBytes,
                            UploadedBy = request.AuthorId,
                            UploadedAt = DateTime.UtcNow
                        }).ToList();

                        await _attachmentRepository.AddRangeAsync(attachments);
                        await _unitOfWork.SaveChangesAsync();
                    }

                    // 6️⃣ Commit transaction
                    await _unitOfWork.CommitAsync();

                    return new BaseResponseDto<bool>
                    {
                        Status = 200,
                        Message = "Post created successfully.",
                        ResponseData = true
                    };
                }
                catch
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                var errorMessage = ex.InnerException != null
                ? ex.InnerException.Message
                : ex.Message;

                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to update post: {errorMessage}",
                    ResponseData = false
                };
            }
        }
    }
}
