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
            string TargetType, // "Post" or "Comment"

            [Required]
            Guid TargetId, // ID of the Post or Comment

            [Required, MinLength(5)]
            string Reason, // The reason for reporting

            string? Details // Optional additional details
        );
    }
}
