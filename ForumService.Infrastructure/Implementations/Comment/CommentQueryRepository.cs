// Gợi ý đường dẫn: ForumService.Infrastructure/Implementations/Comment/CommentQueryRepository.cs
using Dapper;
using ForumService.Contract.TransferObjects.Comment;
using ForumService.Core.Interfaces.Comment;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations.Comment
{
    public class CommentQueryRepository : ICommentQueryRepository
    {
        private readonly string _connectionString;

        public CommentQueryRepository(IConfiguration configuration)
        {

            _connectionString = configuration.GetConnectionString("ForumDb")
                ?? throw new InvalidOperationException("Connection string 'ForumDb' not found.");
        }

        /// <summary>
        /// Retrieves all non-deleted comments for a specific post.
        /// </summary>
        public async Task<IEnumerable<Domain.Models.Comment>> GetCommentsByPostIdAsync(Guid postId, CancellationToken cancellationToken)
        {

            await using var connection = new NpgsqlConnection(_connectionString);

            const string sql = @"
SELECT
    c.""CommentId"",
    c.""AuthorId"",
    c.""ParentCommentId"",
    c.""Content"",
    c.""UpvoteCount"",
    c.""DownvoteCount"",
    c.""CreatedAt""
FROM ""Comments"" c
WHERE c.""PostId"" = @PostId AND c.""IsDeleted"" = FALSE
ORDER BY c.""CreatedAt"" ASC;
";

            var command = new CommandDefinition(sql, new { PostId = postId }, cancellationToken: cancellationToken);

            return await connection.QueryAsync<Domain.Models.Comment>(command);
        }
    }
}