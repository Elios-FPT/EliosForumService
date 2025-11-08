using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Upload
{
    public static class Command
    {
        /// <summary>
        /// Command to upload one or more files temporarily.
        /// </summary>
        public record UploadFilesCommand(
            Guid UploadedByUserId,
            List<FileToUploadDto> FilesToUpload
        ) : ICommand<BaseResponseDto<List<UploadFileResponseDto>>>;

        public record DeleteAttachmentCommand(
        Guid AttachmentId,
        Guid UserId 
        ) : ICommand<BaseResponseDto<bool>>;
    }
}
