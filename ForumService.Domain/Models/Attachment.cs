using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    public class Attachment
    {
        [Key]
        public Guid AttachmentId { get; set; }
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public string Filename { get; set; } = null!;
        public string Url { get; set; } = null!;
        public string? ContentType { get; set; }
        public long? SizeBytes { get; set; }
        public Guid? UploadedBy { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
