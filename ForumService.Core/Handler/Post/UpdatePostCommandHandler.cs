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
    public class UpdatePostCommandHandler : ICommandHandler<UpdatePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            IUnitOfWork unitOfWork)
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
        {
            // 1️⃣ Validate
            if (request.PostId == Guid.Empty)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "PostId cannot be empty.",
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
                await _unitOfWork.BeginTransactionAsync();

                try
                {
                    // 2️⃣ Lấy post hiện có
                    var post = await _postRepository.GetByIdAsync(request.PostId);
                    if (post == null)
                    {
                        return new BaseResponseDto<bool>
                        {
                            Status = 404,
                            Message = $"Post with ID {request.PostId} not found.",
                            ResponseData = false
                        };
                    }

                    // 3️⃣ Cập nhật thông tin bài viết
                    post.Title = request.Title;
                    post.Summary = request.Summary;
                    post.Content = request.Content;
                    post.CategoryId = request.CategoryId;
                    post.Status = request.Status ?? post.Status;
                    post.UpdatedAt = DateTime.UtcNow;

                    await _postRepository.UpdateAsync(post);

                    // 4️⃣ Xử lý attachments
                    if (request.Attachments is not null)
                    {
                        // Xóa attachments cũ (nếu có)
                        var oldAttachments = await _attachmentRepository
                            .GetListAsync(a => a.TargetId == post.PostId && a.TargetType == "Post");

                        if (oldAttachments.Any())
                            await _attachmentRepository.DeleteRangeAsync(oldAttachments);

                        // Thêm attachments mới
                        var newAttachments = request.Attachments.Select(a => new Domain.Models.Attachment
                        {
                            AttachmentId = Guid.NewGuid(),
                            TargetType = "Post",
                            TargetId = post.PostId,
                            Filename = a.Filename,
                            Url = a.Url,
                            ContentType = a.ContentType,
                            SizeBytes = a.SizeBytes,
                            UploadedBy = post.AuthorId,
                            UploadedAt = DateTime.UtcNow
                        }).ToList();

                        if (newAttachments.Any())
                            await _attachmentRepository.AddRangeAsync(newAttachments);
                            await _unitOfWork.SaveChangesAsync();
                    }

                    // 5️⃣ Commit transaction
                    await _unitOfWork.CommitAsync();

                    return new BaseResponseDto<bool>
                    {
                        Status = 200,
                        Message = "Post updated successfully.",
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
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to update post: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}
