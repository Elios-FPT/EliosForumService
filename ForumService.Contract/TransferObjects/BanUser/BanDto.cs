using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects.BanUser
{
    /// <summary>
    /// Data Transfer Object for displaying user ban details.
    /// </summary>
    public class BanDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        // Optional: Include User details if needed for UI display
        public string? UserFirstName { get; set; }
        public string? UserLastName { get; set; }
        public string? UserAvatarUrl { get; set; }

        public string Reason { get; set; } = null!;

        public Guid BannedBy { get; set; }
        public string? BannedByFirstName { get; set; }
        public string? BannedByLastName { get; set; }

        public DateTime BannedAt { get; set; }
        public DateTime? BanUntil { get; set; }

        public bool IsActive { get; set; }

        // Helper to determine if ban is permanent
        public bool IsPermanent => !BanUntil.HasValue;

        // Unban details (if applicable)
        public DateTime? UnbannedAt { get; set; }
        public Guid? UnbannedBy { get; set; }
        public string? UnbanReason { get; set; }
    }
}
