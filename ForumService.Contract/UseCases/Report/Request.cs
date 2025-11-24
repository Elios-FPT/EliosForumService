using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Report
{
    public static class Request
    {
        /// <summary>
        /// Request to create a new report.
        /// </summary>
        public record CreateReportRequest(
            [Required]
            [RegularExpression("^(Post|Comment)$", ErrorMessage = "TargetType must be either 'Post' or 'Comment'.")]
            string TargetType, // "Post" or "Comment"

            [Required]
            Guid TargetId, // ID of the Post or Comment

            [Required, MinLength(5, ErrorMessage = "Reason must be at least 5 characters long.")]
            [MaxLength(500, ErrorMessage = "Reason cannot exceed 500 characters.")]
            string Reason, // The reason for reporting

            [MaxLength(1000, ErrorMessage = "Details cannot exceed 1000 characters.")]
            string? Details // Optional additional details
        );

        /// <summary>
        /// Request to update/resolve a report (For Moderators).
        /// </summary>
        public record ResolveReportRequest(
            [Required]
            [RegularExpression("^(Approved|Rejected)$", ErrorMessage = "Status must be 'Approved' or 'Rejected'.")]
            string Status, // The new status decided by the moderator

            bool DeleteContent,
            [MaxLength(1000)]
            string? ModeratorNote // Note explaining the decision
        );

        /// <summary>
        /// Request to get a single report by ID.
        /// </summary>
        public record GetReportByIdRequest(
            Guid ReportId
        );

        /// <summary>
        /// Request to get a paginated list of reports with filters (For Moderators).
        /// </summary>
        public record GetReportsRequest(
            // Filtering
            string? TargetType = null,      // Filter by "Post" or "Comment"
            string? Status = null,          // Filter by "Pending", "Resolved", "Rejected"
            Guid? ReporterId = null,        // Filter by who reported it
            Guid? TargetId = null,          // Filter reports for a specific content

            // Pagination
            [Range(1, 100)]
            int Limit = 20,                 // Page size

            [Range(0, int.MaxValue)]
            int Offset = 0,                 // Skip count (or PageNumber depending on your mapping logic)

            // Sorting
            string? SortBy = null,          // e.g., "CreatedAt"
            string? SortOrder = null        // e.g., "ASC", "DESC"
        );

    }
}
