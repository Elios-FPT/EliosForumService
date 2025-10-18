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
    public class CreatePostCommandHandler : ICommandHandler<CreatePostCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        private readonly IUnitOfWork _unitOfWork;
        // THÊM VÀO: HTTP Client để gọi service khác
        private readonly ISUtilityServiceClient _utilityServiceClient;

        public CreatePostCommandHandler(
            IGenericRepository<Domain.Models.Post> postRepository,
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient) // Dependency Injection
        {
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
        }

        public async Task<BaseResponseDto<bool>> Handle(CreatePostCommand request, CancellationToken cancellationToken)
        {
            // 1️⃣ Validate input
            if (request.AuthorId == Guid.Empty || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            {
                return new BaseResponseDto<bool> { Status = 400, Message = "AuthorId, Title, and Content cannot be empty.", ResponseData = false };
            }

            // Bắt đầu transaction để đảm bảo tính toàn vẹn dữ liệu
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 2️⃣ Create Post entity
                var post = new Domain.Models.Post
                {
                    PostId = Guid.NewGuid(),
                    AuthorId = request.AuthorId,
                    CategoryId = request.CategoryId,
                    Title = request.Title,
                    Summary = request.Summary,
                    Content = request.Content,
                    PostType = request.PostType ?? "Post",
                    Status = "Draft",
                    IsDeleted = false,
                    IsFeatured = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.AuthorId
                };

                // Thêm Post vào context nhưng chưa commit
                await _postRepository.AddAsync(post);

                // 3️⃣ Upload files và tạo Attachments
                if (request.FilesToUpload is not null && request.FilesToUpload.Any())
                {
                    var attachments = new List<Domain.Models.Attachment>();
                    foreach (var file in request.FilesToUpload)
                    {
                        // Gọi SUtilityService để upload file
                        var keyPrefix = $"posts/{post.PostId}";
                        var uploadedUrl = await _utilityServiceClient.UploadFileAsync(keyPrefix, file, cancellationToken);

                        // NẾU UPLOAD THẤT BẠI: HỦY GIAO DỊCH VÀ BÁO LỖI NGAY LẬP TỨC
                        if (string.IsNullOrEmpty(uploadedUrl))
                        {
                            await _unitOfWork.RollbackAsync();
                            return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to upload file: {file.FileName}. Post creation cancelled.", ResponseData = false };
                        }

                        // Nếu thành công, tạo entity Attachment với URL trả về
                        attachments.Add(new Domain.Models.Attachment
                        {
                            AttachmentId = Guid.NewGuid(),
                            TargetType = "Post",
                            TargetId = post.PostId,
                            Filename = file.FileName,
                            Url = uploadedUrl, // <-- URL thật từ service upload
                            ContentType = file.ContentType,
                            SizeBytes = file.Content.Length,
                            UploadedBy = request.AuthorId,
                            UploadedAt = DateTime.UtcNow
                        });
                    }

                    // Thêm danh sách attachments vào context
                    await _attachmentRepository.AddRangeAsync(attachments);
                }

                // 4️⃣ Commit tất cả thay đổi vào database
                // Chỉ được gọi khi tất cả các bước trên đã thành công
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = "Post created successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                // Nếu có bất kỳ lỗi nào khác xảy ra, rollback transaction
                await _unitOfWork.RollbackAsync();

                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                // Cân nhắc ghi log lỗi ở đây (ILogger)
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to create post: {errorMessage}", ResponseData = false };
            }
        }
    }
}

