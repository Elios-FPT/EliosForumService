using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects
{
    public class UploadFileResponseDto
    {
        public Guid AttachmentId { get; set; }
        public string FileName { get; set; }
        public string Url { get; set; }
        public string ContentType { get; set; }
        public long? SizeBytes { get; set; }
    }
}
