using ForumService.Contract.TransferObjects.Comment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Interfaces.Comment
{
    /// <summary>
    /// Defines the contract for query operations related to Comments,
    /// intended for Dapper implementation.
    /// </summary>
    public interface ICommentQueryRepository
    {
        /// <summary>
        /// Retrieves all non-deleted comments for a specific post,
        /// ordered by creation time.
        /// </summary>
        /// <param name="postId">The ID of the post.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A collection of CommentDto objects.</returns>
        Task<IEnumerable<Domain.Models.Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken);
    }
}
