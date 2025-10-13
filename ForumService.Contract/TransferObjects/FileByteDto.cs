using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects
{
    public class FileByteDto
    {
        public string FileName { get; set; }

        public string ContentType { get; set; }

        public byte[] FileContents { get; set; }
    }
}
