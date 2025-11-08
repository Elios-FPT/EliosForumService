using Dapper;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Core.Interfaces.Post;
using ForumService.Domain.Models;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Infrastructure.Implementations.Post
{
    public class PostQueryRepository : IPostQueryRepository
    {
        private readonly string _connectionString;

        public PostQueryRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("ForumDb")
                ?? throw new InvalidOperationException("Connection string 'ForumDb' not found.");
        }

        public async Task<IEnumerable<Domain.Models.Post>> GetPublicViewPostsAsync(GetPublicViewPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT
                    p.""PostId"", p.""AuthorId"", p.""CategoryId"", p.""Title"", p.""Summary"", p.""Content"", p.""Status"",
                    p.""ViewsCount"", p.""CommentCount"", p.""UpvoteCount"", p.""DownvoteCount"", p.""CreatedAt"", p.""IsDeleted"", p.""PostType"",
                    p.""ModeratedBy"", p.""ModeratedAt"", p.""RejectionReason"", p.""UpdatedAt"", p.""DeletedAt"", p.""DeletedBy"",p.""CreatedBy"",p.""UpdatedBy"",p.""IsFeatured"",
                    c.""Name"" AS CategoryName,
                    (SELECT STRING_AGG(t.""Name"", ',') FROM ""Tags"" t JOIN ""PostTags"" pt ON t.""TagId"" = pt.""TagId"" WHERE pt.""PostId"" = p.""PostId"") AS Tags
                FROM ""Posts"" p
                LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId""
                ");

            var whereClauses = new List<string>
            {
                @"p.""Status"" = 'Published'",
                @"p.""IsDeleted"" = FALSE"
            };

            if (request.AuthorId.HasValue)
            {
                whereClauses.Add(@"p.""AuthorId"" = @AuthorId");
                parameters.Add("AuthorId", request.AuthorId.Value);
            }
            if (request.CategoryId.HasValue)
            {
                whereClauses.Add(@"p.""CategoryId"" = @CategoryId");
                parameters.Add("CategoryId", request.CategoryId.Value);
            }
            if (!string.IsNullOrWhiteSpace(request.PostType))
            {
                whereClauses.Add(@"p.""PostType"" = @PostType");
                parameters.Add("PostType", request.PostType);
            }
            if (!string.IsNullOrWhiteSpace(request.SearchKeyword))
            {
                whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword OR p.""Summary"" ILIKE @SearchKeyword)");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch
            {
                "viewscount" => @"p.""ViewsCount""",
                "upvotecount" => @"p.""UpvoteCount""",
                "createdat" => @"p.""CreatedAt""",
                _ => @"p.""CreatedAt"""
            };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Limit);
            parameters.Add("Offset", request.Offset);

            return await connection.QueryAsync<Domain.Models.Post>(sqlBuilder.ToString(), parameters);
        }

        public async Task<IEnumerable<Domain.Models.Post>> GetModeratorPublicViewPostsAsync(GetModeratorPublicPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT
                    p.""PostId"", p.""AuthorId"", p.""CategoryId"", p.""Title"", p.""Summary"", p.""Content"", p.""Status"",
                    p.""ViewsCount"", p.""CommentCount"", p.""UpvoteCount"", p.""DownvoteCount"", p.""CreatedAt"", p.""IsDeleted"", p.""PostType"",
                    p.""ModeratedBy"", p.""ModeratedAt"", p.""RejectionReason"", p.""UpdatedAt"", p.""DeletedAt"", p.""DeletedBy"",p.""CreatedBy"",p.""UpdatedBy"",p.""IsFeatured"",
                    c.""Name"" AS CategoryName,
                    (SELECT STRING_AGG(t.""Name"", ',') FROM ""Tags"" t JOIN ""PostTags"" pt ON t.""TagId"" = pt.""TagId"" WHERE pt.""PostId"" = p.""PostId"") AS Tags
                FROM ""Posts"" p
                LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId""
                ");

            var whereClauses = new List<string>
            {
                @"p.""Status"" = 'Published'",
                @"p.""IsDeleted"" = FALSE"
            };

            if (request.AuthorId.HasValue)
            {
                whereClauses.Add(@"p.""AuthorId"" = @AuthorId");
                parameters.Add("AuthorId", request.AuthorId.Value);
            }
            if (request.CategoryId.HasValue)
            {
                whereClauses.Add(@"p.""CategoryId"" = @CategoryId");
                parameters.Add("CategoryId", request.CategoryId.Value);
            }
            if (!string.IsNullOrWhiteSpace(request.PostType))
            {
                whereClauses.Add(@"p.""PostType"" = @PostType");
                parameters.Add("PostType", request.PostType);
            }
            if (!string.IsNullOrWhiteSpace(request.SearchKeyword))
            {
                whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword OR p.""Summary"" ILIKE @SearchKeyword)");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch
            {
                "viewscount" => @"p.""ViewsCount""",
                "upvotecount" => @"p.""UpvoteCount""",
                "createdat" => @"p.""CreatedAt""",
                _ => @"p.""CreatedAt"""
            };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Limit);
            parameters.Add("Offset", request.Offset);

            return await connection.QueryAsync<Domain.Models.Post>(sqlBuilder.ToString(), parameters);
        }

        public async Task<IEnumerable<Domain.Models.Post>> GetPendingPostsAsync(GetPendingPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT
                    p.""PostId"", p.""AuthorId"", p.""CategoryId"", p.""Title"", p.""Summary"", p.""Content"", p.""Status"",
                    p.""ViewsCount"", p.""CommentCount"", p.""UpvoteCount"", p.""DownvoteCount"", p.""CreatedAt"", p.""IsDeleted"", p.""PostType"",
                    p.""ModeratedBy"", p.""ModeratedAt"", p.""RejectionReason"", p.""UpdatedAt"", p.""DeletedAt"", p.""DeletedBy"", p.""CreatedBy"", p.""UpdatedBy"", p.""IsFeatured"",
                    c.""Name"" AS CategoryName,
                    (SELECT STRING_AGG(t.""Name"", ',') FROM ""Tags"" t JOIN ""PostTags"" pt ON t.""TagId"" = pt.""TagId"" WHERE pt.""PostId"" = p.""PostId"") AS Tags
                FROM ""Posts"" p
                LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId""
                ");

            var whereClauses = new List<string>
    {
                @"p.""Status"" = 'PendingReview'",
                @"p.""IsDeleted"" = FALSE"
    };

            if (!string.IsNullOrWhiteSpace(request.PostType))
            {
                whereClauses.Add(@"p.""PostType"" = @PostType");
                parameters.Add("PostType", request.PostType);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchKeyword))
            {
                whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword OR p.""Summary"" ILIKE @SearchKeyword)");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch
            {
                "viewscount" => @"p.""ViewsCount""",
                "upvotecount" => @"p.""UpvoteCount""",
                "createdat" => @"p.""CreatedAt""",
                _ => @"p.""CreatedAt"""
            };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Limit);
            parameters.Add("Offset", request.Offset);

            return await connection.QueryAsync<Domain.Models.Post>(sqlBuilder.ToString(), parameters);
        }


        public async Task<IEnumerable<Domain.Models.Post>> GetArchivedPostsAsync(GetArchivedPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT
                    p.""PostId"", p.""AuthorId"", p.""CategoryId"", p.""Title"", p.""Summary"", p.""Content"", p.""Status"",
                    p.""ViewsCount"", p.""CommentCount"", p.""UpvoteCount"", p.""DownvoteCount"", p.""CreatedAt"", p.""IsDeleted"", p.""PostType"",
                    p.""ModeratedBy"", p.""ModeratedAt"", p.""RejectionReason"", p.""UpdatedAt"", p.""DeletedAt"", p.""DeletedBy"", p.""CreatedBy"", p.""UpdatedBy"", p.""IsFeatured"",
                    c.""Name"" AS CategoryName,
                    (SELECT STRING_AGG(t.""Name"", ',') FROM ""Tags"" t JOIN ""PostTags"" pt ON t.""TagId"" = pt.""TagId"" WHERE pt.""PostId"" = p.""PostId"") AS Tags
                FROM ""Posts"" p
                LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId""
                ");

            var whereClauses = new List<string>
                {
                    @"(p.""Status"" = 'Rejected' OR p.""IsDeleted"" = TRUE)"
                };

            if (!string.IsNullOrWhiteSpace(request.PostType))
            {
                whereClauses.Add(@"p.""PostType"" = @PostType");
                parameters.Add("PostType", request.PostType);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchKeyword))
            {
                whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword OR p.""Summary"" ILIKE @SearchKeyword)");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch
            {
                "updatedat" => @"p.""UpdatedAt""",
                "deletedat" => @"p.""DeletedAt""",
                "createdat" => @"p.""CreatedAt""",
                _ => @"p.""UpdatedAt"""
            };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Limit);
            parameters.Add("Offset", request.Offset);

            return await connection.QueryAsync<Domain.Models.Post>(sqlBuilder.ToString(), parameters);
        }


        public async Task<IEnumerable<Domain.Models.Post>> GetMyPostsAsync(GetMyPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT
                    p.""PostId"", p.""AuthorId"", p.""CategoryId"", p.""Title"", p.""Summary"", p.""Content"", p.""Status"",
                    p.""ViewsCount"", p.""CommentCount"", p.""UpvoteCount"", p.""DownvoteCount"", p.""CreatedAt"",
                    c.""Name"" AS CategoryName,
                    (SELECT STRING_AGG(t.""Name"", ', ') FROM ""Tags"" t JOIN ""PostTags"" pt ON t.""TagId"" = pt.""TagId"" WHERE pt.""PostId"" = p.""PostId"") AS Tags
                FROM ""Posts"" p
                LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId""");

            var whereClauses = new List<string> { @"p.""AuthorId"" = @RequesterId", @"p.""IsDeleted"" = FALSE" };
            parameters.Add("RequesterId", request.RequesterId);

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                whereClauses.Add(@"p.""Status"" = @Status");
                parameters.Add("Status", request.Status);
            }
            if (request.CategoryId.HasValue)
            {
                whereClauses.Add(@"p.""CategoryId"" = @CategoryId");
                parameters.Add("CategoryId", request.CategoryId.Value);
            }
            if (!string.IsNullOrWhiteSpace(request.PostType))
            {
                whereClauses.Add(@"p.""PostType"" = @PostType");
                parameters.Add("PostType", request.PostType);
            }
            if (!string.IsNullOrWhiteSpace(request.SearchKeyword))
            {
                whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword)");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            if (whereClauses.Any())
            {
                sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));
            }

            var sortBy = request.SortBy?.ToLower() switch
            {
                "viewscount" => @"p.""ViewsCount""",
                "createdat" => @"p.""CreatedAt""",
                _ => @"p.""CreatedAt"""
            };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Limit);
            parameters.Add("Offset", request.Offset);

            return await connection.QueryAsync<Domain.Models.Post>(sqlBuilder.ToString(), parameters);
        }
    }
}

