using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Contract.UseCases.Upload;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Upload.Request;


namespace ForumService.Core.Handler.Upload
{
    public class GetMyUploadedImagesQueryHandler : IQueryHandler<GetMyUploadedImagesQuery, BaseResponseDto<List<UploadFileResponseDto>>>
    {
        private readonly IGenericRepository<Domain.Models.Attachment> _attachmentRepository;

        public GetMyUploadedImagesQueryHandler(IGenericRepository<Domain.Models.Attachment> attachmentRepository)
        {
            _attachmentRepository = attachmentRepository;
        }

        public async Task<BaseResponseDto<List<UploadFileResponseDto>>> Handle(GetMyUploadedImagesQuery request, CancellationToken cancellationToken)
        {
            if (request.UserId == Guid.Empty)
            {
                return new BaseResponseDto<List<UploadFileResponseDto>> { Status = 400, Message = "User ID is required." };
            }

            try
            {
                // Use GetListAsyncUntracked for better performance on read-only queries
                // and use Selector to directly map entities to DTOs
                var images = await _attachmentRepository.GetListAsyncUntracked(
                    // 1. Filter by User ID AND ContentType starting with "image/"
                    filter: a =>
                        a.UploadedBy == request.UserId &&
                        a.ContentType != null &&
                        a.ContentType.StartsWith("image/"),

                    // 2. Map (project) to DTO
                    selector: a => new UploadFileResponseDto
                    {
                        AttachmentId = a.AttachmentId,
                        FileName = a.Filename,
                        Url = a.Url,
                        ContentType = a.ContentType,
                        SizeBytes = a.SizeBytes ?? 0
                    },

                    // 3. Optional sorting: newest images first
                    orderBy: q => q.OrderByDescending(a => a.UploadedAt)
                );

                return new BaseResponseDto<List<UploadFileResponseDto>>
                {
                    Status = 200,
                    Message = "Successfully retrieved uploaded images.",
                    ResponseData = images.ToList()
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<List<UploadFileResponseDto>>
                {
                    Status = 500,
                    Message = $"Failed to retrieve images: {ex.Message}"
                };
            }
        }
    }
}
