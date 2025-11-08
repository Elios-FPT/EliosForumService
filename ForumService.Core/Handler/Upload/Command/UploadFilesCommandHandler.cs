using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Upload.Command;

namespace ForumService.Core.Handler.Upload.Command
{
    public class UploadFilesCommandHandler : ICommandHandler<UploadFilesCommand, BaseResponseDto<List<UploadFileResponseDto>>>
    {
        private readonly IGenericRepository<Attachment> _attachmentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient;

        public UploadFilesCommandHandler(
            IGenericRepository<Attachment> attachmentRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient)
        {
            _attachmentRepository = attachmentRepository;
            _unitOfWork = unitOfWork;
            _utilityServiceClient = utilityServiceClient;
        }

        public async Task<BaseResponseDto<List<UploadFileResponseDto>>> Handle(UploadFilesCommand request, CancellationToken cancellationToken)
        {
            if (request.FilesToUpload == null || !request.FilesToUpload.Any())
            {
                return new BaseResponseDto<List<UploadFileResponseDto>> { Status = 400, Message = "No files to upload." };
            }

            var uploadedFiles = new List<UploadFileResponseDto>();
            var attachmentsToCreate = new List<Attachment>();

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                foreach (var file in request.FilesToUpload)
                {
                    // 1. Upload file to cloud storage
                    var keyPrefix = $"temp_uploads/{request.UploadedByUserId}";
                    var uploadedUrl = await _utilityServiceClient.UploadFileAsync(keyPrefix, file, cancellationToken);

                    if (string.IsNullOrEmpty(uploadedUrl))
                    {
                        throw new Exception($"Failed to upload file: {file.FileName}");
                    }

                    // 2. Create Attachment entity (unlinked)
                    var attachment = new Attachment
                    {
                        AttachmentId = Guid.NewGuid(),
                        TargetType = "Temp", // Mark as "Temp" or "Unlinked"
                        TargetId = null,     // No PostId yet
                        Filename = file.FileName,
                        Url = uploadedUrl,
                        ContentType = file.ContentType,
                        SizeBytes = file.Content.Length,
                        UploadedBy = request.UploadedByUserId,
                        UploadedAt = DateTime.UtcNow
                    };

                    attachmentsToCreate.Add(attachment);

                    uploadedFiles.Add(new UploadFileResponseDto
                    {
                        AttachmentId = attachment.AttachmentId,
                        FileName = attachment.Filename,
                        Url = attachment.Url,
                        ContentType = attachment.ContentType,
                        SizeBytes = attachment.SizeBytes
                    });
                }

                // 3. Save Attachment records to DB
                await _attachmentRepository.AddRangeAsync(attachmentsToCreate);
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<List<UploadFileResponseDto>>
                {
                    Status = 200,
                    Message = "Files uploaded successfully.",
                    ResponseData = uploadedFiles
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<List<UploadFileResponseDto>>
                {
                    Status = 500,
                    Message = $"Failed to upload files: {ex.Message}"
                };
            }
        }
    }
}
