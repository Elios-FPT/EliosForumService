using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.BanUser
{
    public static class Request
    {
        /// <summary>
        /// Request payload for banning a user.
        /// </summary>
        public record CreateBanUserRequest(
            [Required]
            Guid UserId, // The ID of the user to be banned

            [Required]
            [MinLength(5, ErrorMessage = "Lý do phải dài ít nhất 5 ký tự.")]
            [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
            string Reason,

            /// <summary>
            /// Date and time when the ban expires. If null, the ban is permanent.
            /// </summary>
            DateTime? BanUntil
        );

        /// <summary>
        /// Request to retrieve a paginated list of banned users.
        /// </summary>
        public record GetBannedUsersRequest(
            // Filters
            Guid? UserId = null,         // Filter by specific banned user
            bool? IsActive = null,       // Filter by Active/Inactive bans

            // Pagination
            [Range(1, 100)]
            int Limit = 20,

            [Range(0, int.MaxValue)]
            int Offset = 0
        );

        /// <summary>
        /// Request to remove ban for user.
        /// </summary>
        public record UnbanUserRequest(
            [MaxLength(500, ErrorMessage = "Lý do gỡ cấm không được vượt quá 500 ký tự.")]
            string? UnbanReason 
        );

        /// <summary>
        /// Request to update ban information (e.g., extend or change reason).
        /// </summary>
        public record UpdateBanRequest(
            [Required]
            [MinLength(5, ErrorMessage = "Lý do phải dài ít nhất 5 ký tự.")]
            [MaxLength(500, ErrorMessage = "Lý do không được vượt quá 500 ký tự.")]
            string Reason,

            DateTime? BanUntil // Thời hạn mới (null = vĩnh viễn)
        );
    }
}
