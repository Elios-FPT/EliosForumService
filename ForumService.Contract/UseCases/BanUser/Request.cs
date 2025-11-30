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
            [MinLength(5, ErrorMessage = "Reason must be at least 5 characters long.")]
            [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
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
            Guid? UserId = null,
            bool? IsActive = null,

            int Page = 1,
            int Size = 10
        );

        /// <summary>
        /// Request to remove ban for user.
        /// </summary>
        public record UnbanUserRequest(
            [MaxLength(500, ErrorMessage = "Unban reason cannot exceed 500 characters.")]
            string? UnbanReason 
        );

        /// <summary>
        /// Request to update ban information (e.g., extend or change reason).
        /// </summary>
        public record UpdateBanRequest(
            [Required]
               [MinLength(5, ErrorMessage = "Reason must be at least 5 characters long.")]
            [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
            string Reason,

            DateTime? BanUntil // New ban duration (null = permanent)
        );
    }
}
