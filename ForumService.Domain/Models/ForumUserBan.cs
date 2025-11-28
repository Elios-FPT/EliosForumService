using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Domain.Models
{
    [Table("ForumUserBans")]
    public class ForumUserBan
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; } // ID of the banned user

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } // Reason (Spam, Offensive language, etc.)

        [Required]
        public Guid BannedBy { get; set; } // ID of the Admin/Moderator who performed the ban

        [Required]
        public DateTime BannedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Expiration time of the Ban. If null, it means a permanent Ban.
        /// </summary>
        public DateTime? BanUntil { get; set; }

        /// <summary>
        /// Status of the ban.
        /// True: Currently active.
        /// False: Unbanned before expiration.
        /// </summary>
        public bool IsActive { get; set; } = true;

        // Audit fields for unbanning (Optional)
        public DateTime? UnbannedAt { get; set; }
        public Guid? UnbannedBy { get; set; }
        public string? UnbanReason { get; set; }

        // Helper property for quick check (not mapped to DB)
        [NotMapped]
        public bool IsExpired => IsActive && BanUntil.HasValue && BanUntil.Value < DateTime.UtcNow;
    }
}
