using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Core.Handler.Post.Command
{
    public class UpdatePostCommandHandler : ICommandHandler<UpdatePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient;

        public UpdatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient)
        {
            _postRepository = postRepository;
            _attachmentRepository = attachmentRepository;
            _unitOfWork = unitOfWork;
            _utilityServiceClient = utilityServiceClient;
        }

        public async Task<BaseResponseDto<bool>> Handle(UpdatePostCommand request, CancellationToken cancellationToken)
        {
            // 1️⃣ Validate input
            if (request.PostId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "PostId, Title, and Content cannot be empty.", ResponseData = false };
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 2️⃣ Lấy post hiện có
                var post = await _postRepository.GetByIdAsync(request.PostId);
                if (post == null)
                {
                    await _unitOfWork.RollbackAsync();
                    return new BaseResponseDto<bool> { Status = 404, Message = $"Post with ID {request.PostId} not found.", ResponseData = false };
                }

                // 3️⃣ Xử lý xóa attachments cũ
                if (request.AttachmentIdsToDelete is not null && request.AttachmentIdsToDelete.Any())
                {
                    var attachmentsToDelete = await _attachmentRepository.GetListAsync(
                        filter: a => a.TargetId == post.PostId && request.AttachmentIdsToDelete.Contains(a.AttachmentId)
                    );

                    if (attachmentsToDelete.Any())
                    {
                        // TODO: Gọi SUtilityService để xóa file vật lý trên storage.
                        // Việc này quan trọng để tránh file rác. Bạn sẽ cần một endpoint DeleteFile trong SUtilityService.
                        // foreach (var attachment in attachmentsToDelete) { ... await _utilityServiceClient.DeleteFileAsync(...); }

                        await _attachmentRepository.DeleteRangeAsync(attachmentsToDelete);
                    }
                }

                // 4️⃣ Xử lý upload attachments mới
                if (request.NewFilesToUpload is not null && request.NewFilesToUpload.Any())
                {
                    var newAttachments = new List<Domain.Models.Attachment>();
                    foreach (var file in request.NewFilesToUpload)
                    {
                        var keyPrefix = $"posts/{post.PostId}";
                        var uploadedUrl = await _utilityServiceClient.UploadFileAsync(keyPrefix, file, cancellationToken);

                        if (string.IsNullOrEmpty(uploadedUrl))
                        {
                            await _unitOfWork.RollbackAsync();
                            return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to upload new file: {file.FileName}. Update cancelled.", ResponseData = false };
                        }

                        newAttachments.Add(new Domain.Models.Attachment
                        {
                            AttachmentId = Guid.NewGuid(),
                            TargetType = "Post",
                            TargetId = post.PostId,
                            Filename = file.FileName,
                            Url = uploadedUrl,
                            ContentType = file.ContentType,
                            SizeBytes = file.Content.Length,
                            UploadedBy = post.AuthorId, // Hoặc người dùng đang thực hiện request
                            UploadedAt = DateTime.UtcNow
                        });
                    }
                    await _attachmentRepository.AddRangeAsync(newAttachments);
                }

                // 5️⃣ Cập nhật thông tin bài viết
                post.Title = request.Title;
                post.Summary = request.Summary;
                post.Content = request.Content;
                post.CategoryId = request.CategoryId;
                post.Status = "Draw";
                post.UpdatedAt = DateTime.UtcNow;
                // post.UpdatedBy = ... ; // Lấy từ thông tin user hiện tại

                await _postRepository.UpdateAsync(post);

                // 6️⃣ Commit transaction
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = "Post updated successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to update post: {ex.Message}", ResponseData = false };
            }
        }
    }
}
