using ForumService.Contract.TransferObjects.Tag;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.TransferObjects.Post
{
    /// <summary>
    /// Represents the detailed view of a post specifically for moderators,
    /// including author, moderation details, and moderator information.
    /// </summary>
    public class ModeratorPostViewDto
    {
        public Guid PostId { get; set; }
        public Guid AuthorId { get; set; } = Guid.Empty; // ID of the original author
        public Guid? CategoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; } // Summary might be useful for quick review
        public string Content { get; set; } = string.Empty; // Content is needed for moderation
        public string PostType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Important for moderator view (Pending, Rejected, etc.)
        public long ViewsCount { get; set; }
        public int CommentCount { get; set; }
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsDeleted { get; set; } // Moderators might need to see soft-deleted posts
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; } // Show last updated time

        // --- Enriched Fields ---
        public string? CategoryName { get; set; }

        public string? AuthorFirstName { get; set; }
        public string? AuthorLastName { get; set; }
        public string? AuthorAvatarUrl { get; set; }
        public string AuthorFullName => $"{AuthorFirstName} {AuthorLastName}".Trim();

        // --- Moderation Fields ---
        public Guid? ModeratedBy { get; set; } // ID of the moderator
        public DateTime? ModeratedAt { get; set; }
        public string? RejectionReason { get; set; }

        // --- Moderator Details (Enriched from UserService based on ModeratedBy) ---
        public string? ModeratorFirstName { get; set; }
        public string? ModeratorLastName { get; set; }
        public string? ModeratorAvatarUrl { get; set; }
        public string ModeratorFullName => $"{ModeratorFirstName} {ModeratorLastName}".Trim();

        // --- Soft Deletion Fields ---
        public Guid? DeletedBy { get; set; } // ID of the user who soft-deleted
        public DateTime? DeletedAt { get; set; }

        // --- NEW DeletedBy User Details (Enriched from UserService based on DeletedBy) ---
        public string? DeletedByFirstName { get; set; }
        public string? DeletedByLastName { get; set; }
        public string? DeletedByAvatarUrl { get; set; }
        public string DeletedByFullName => $"{DeletedByFirstName} {DeletedByLastName}".Trim();
    }
}
