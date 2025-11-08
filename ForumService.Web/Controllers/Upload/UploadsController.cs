using Asp.Versioning;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static ForumService.Contract.UseCases.Upload.Command;
using static ForumService.Contract.UseCases.Upload.Request;

namespace ForumService.Web.Controllers.Upload
{
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Upload")]
    [Route("api/forum/upload")]
    public class UploadsController : ControllerBase
    {
        private readonly ISender _sender; 

        public UploadsController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Uploads files and returns their URLs and AttachmentIds.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<List<UploadFileResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<List<UploadFileResponseDto>>> UploadFiles(List<IFormFile> files)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<List<UploadFileResponseDto>> { Status = 401, Message = "User not authenticated" };
            }

            //var userId = new Guid("102ea1b3-f664-4617-8f43-fdde557f12b6");

            var filesToUpload = new List<FileToUploadDto>();
            if (files != null)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);
                        filesToUpload.Add(new FileToUploadDto
                        {
                            FileName = file.FileName,
                            ContentType = file.ContentType,
                            Content = memoryStream.ToArray()
                        });
                    }
                }
            }

            if (!filesToUpload.Any())
            {
                return new BaseResponseDto<List<UploadFileResponseDto>> { Status = 400, Message = "No valid files provided." };
            }

            var command = new UploadFilesCommand(userId, filesToUpload);
            return await _sender.Send(command);
        }

        /// <summary>
        /// Retrieves all files that are IMAGES (image/*) uploaded by the current user.
        /// </summary>
        [HttpGet("images")]
        [ProducesResponseType(typeof(BaseResponseDto<List<UploadFileResponseDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<BaseResponseDto<List<UploadFileResponseDto>>> GetMyUploadedImages()
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<List<UploadFileResponseDto>> { Status = 401, Message = "User not authenticated" };
            }

            //var userId = new Guid("102ea1b3-f664-4617-8f43-fdde557f12b6");

            // Create a new query
            var query = new GetMyUploadedImagesQuery(userId);

            // Send the query to the handler
            return await _sender.Send(query);
        }


        /// <summary>
        /// Deletes an uploaded file (only the owner of the file can delete it).
        /// </summary>
        /// <param name="attachmentId">The ID of the attachment to delete.</param>
        [HttpDelete("{attachmentId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> DeleteAttachment([FromRoute] Guid attachmentId)
        {
            var userIdHeader = HttpContext.Request.Headers["X-Auth-Request-User"].FirstOrDefault();
            if (string.IsNullOrEmpty(userIdHeader) || !Guid.TryParse(userIdHeader, out var userId))
            {
                return new BaseResponseDto<bool> { Status = 401, Message = "User not authenticated", ResponseData = false };
            }

            //var userId = new Guid("102ea1b3-f664-4617-8f43-fdde557f12b6");

            var command = new DeleteAttachmentCommand(attachmentId, userId);
            return await _sender.Send(command);
        }

    }
}