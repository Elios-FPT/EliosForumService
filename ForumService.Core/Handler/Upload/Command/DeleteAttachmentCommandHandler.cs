using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.UseCases.Upload;
using ForumService.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Upload.Command;

namespace ForumService.Core.Handler.Upload
{
    public class DeleteAttachmentCommandHandler : ICommandHandler<DeleteAttachmentCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteAttachmentCommandHandler(
            IGenericRepository<Domain.Models.Attachment> attachmentRepository,
            IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork)
        {
            _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<bool>> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var attachment = await _attachmentRepository.GetByIdAsync(request.AttachmentId);

                if (attachment == null)
                {
                    return new BaseResponseDto<bool> { Status = 404, Message = "Attachment not found.", ResponseData = false };
                }

                if (attachment.UploadedBy != request.UserId)
                {
                    return new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to delete this attachment.", ResponseData = false };
                }

                if (attachment.TargetId.HasValue && attachment.TargetType == "Post")
                {
                    var linkedPost = await _postRepository.GetByIdAsync(attachment.TargetId.Value);

                    if (linkedPost != null && !linkedPost.IsDeleted && linkedPost.Status == "Published")
                    {
                        return new BaseResponseDto<bool>
                        {
                            Status = 400,  
                            Message = "Cannot delete this attachment because it is used in a published post. Please edit or unpublish the post first.",
                            ResponseData = false
                        };
                    }
                }

                await _attachmentRepository.DeleteAsync(attachment);
                await _unitOfWork.SaveChangesAsync();

                return new BaseResponseDto<bool> { Status = 200, Message = "Attachment deleted successfully.", ResponseData = true };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<bool> { Status = 500, Message = $"Failed to delete attachment: {ex.Message}", ResponseData = false };
            }
        }
    }
}