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
    public static class Request
    {
        public record GetMyUploadedImagesQuery(
           Guid UserId
       ) : IQuery<BaseResponseDto<List<UploadFileResponseDto>>>;
    }
}
