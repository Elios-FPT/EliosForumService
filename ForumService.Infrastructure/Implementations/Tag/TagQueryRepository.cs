using Dapper;
using ForumService.Core.Interfaces.Tag; 
using ForumService.Domain.Models; 
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Infrastructure.Implementations.Tag
{
    public class TagQueryRepository : ITagQueryRepository
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initializes a new instance of the TagQueryRepository.
        /// </summary>
        /// <param name="configuration">Injected configuration to access appsettings.json.</param>
        public TagQueryRepository(IConfiguration configuration)
        {
            // Lấy chuỗi kết nối "ForumDb"
            _connectionString = configuration.GetConnectionString("ForumDb")
                ?? throw new InvalidOperationException("Connection string 'ForumDb' not found.");
        }

        /// <summary>
        /// Gets a list of full tag objects for a specific post using its ID.
        /// </summary>
        /// <param name="postId">The unique identifier for the post.</param>
        /// <param name="cancellationToken">A token to observe while waiting for the task to complete.</param>
        /// <returns>A collection of Tag objects.</returns>

        public async Task<IEnumerable<Domain.Models.Tag>> GetTagNamesByPostIdAsync(Guid postId, CancellationToken cancellationToken = default)
        {
   
            const string sql = @"
                SELECT t.""TagId"", t.""Name"", t.""Slug"", t.""CreatedAt""
                FROM ""Tags"" t
                JOIN ""PostTags"" pt ON t.""TagId"" = pt.""TagId""
                WHERE pt.""PostId"" = @PostId;
            ";

            await using var connection = new NpgsqlConnection(_connectionString);

            var command = new CommandDefinition(
                commandText: sql,
                parameters: new { PostId = postId },
                cancellationToken: cancellationToken
            );

            return await connection.QueryAsync<Domain.Models.Tag>(command);
        }
    }
}