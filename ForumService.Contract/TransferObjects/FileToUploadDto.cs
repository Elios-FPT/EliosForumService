using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects
{
    public class FileToUploadDto
    {
        /// <summary>
        /// Tên gốc của file, ví dụ: "hinh_san_pham.png"
        /// </summary>
        public string FileName { get; set; } = null!;

        /// <summary>
        /// Nội dung thực sự của file, dưới dạng một mảng byte.
        /// </summary>
        public byte[] Content { get; set; } = null!;

        /// <summary>
        /// Loại nội dung (MIME type), ví dụ: "image/png", "application/pdf"
        /// </summary>
        public string ContentType { get; set; } = null!;
    }
}
