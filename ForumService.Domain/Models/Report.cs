using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    public class Report
    {
        [Key]
        public Guid ReportId { get; set; }
        public Guid ReporterId { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public string Reason { get; set; } = null!;
        public string? Details { get; set; }
        public string Status { get; set; } = "Pending";
        public Guid? ResolvedBy { get; set; } // Reference to user ID, fetched from UserService Redis cache
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
