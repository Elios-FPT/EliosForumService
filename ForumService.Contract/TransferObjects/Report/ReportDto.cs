using System;

namespace ForumService.Contract.TransferObjects.Report
{
    /// <summary>
    /// Data Transfer Object for displaying a report, typically in a moderation panel.
    /// Includes hydrated information about the reporter, target author, and resolver.
    /// </summary>
    public class ReportDto
    {
        
        public Guid ReportId { get; set; }

        /// <summary>
        /// The current status of the report (e.g., "Pending", "Resolved", "Dismissed").
        /// </summary>
        public string Status { get; set; } = "Pending";

        /// <summary>
        /// The primary reason for the report (e.g., "Spam", "Hate Speech").
        /// </summary>
        public string Reason { get; set; } = null!;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public string? TargetContentSnippet { get; set; }
        public Guid TargetAuthorId { get; set; }
        public string? TargetAuthorFirstName { get; set; }
        public string? TargetAuthorLastName { get; set; }
        public string? TargetAuthorAvataUrl { get; set; }
        public Guid ReporterId { get; set; }
        public string? ReporterFirstName { get; set; }
        public string? ReporterLastName { get; set; }
        public string? ReporterAvatarUrl { get; set; }
        public string? ResolvedByFirstName { get; set; }
        public string? ResolvedByLastName { get; set; }
    }
}
