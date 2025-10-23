using Dapper;
using ForumService.Contract.TransferObjects.Comment;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Post;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Infrastructure.Implementations
{
    public class PostQueryRepository : IPostQueryRepository
    {
        private readonly string _connectionString;

        public PostQueryRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("ForumDb")
                ?? throw new InvalidOperationException("Connection string 'ForumDb' not found.");
        }

        public async Task<IEnumerable<PostViewDto>> GetPublicViewPostsAsync(GetPublicViewPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
SELECT
    p.""PostId"", p.""AuthorId"", p.""Title"", p.""Summary"", p.""Content"",
    p.""ViewsCount"", p.""CommentCount"", p.""UpvoteCount"", p.""DownvoteCount"", p.""CreatedAt"",
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
            if (request.Tags != null && request.Tags.Any())
            {
                whereClauses.Add(@"p.""PostId"" IN (SELECT pt_inner.""PostId"" FROM ""PostTags"" pt_inner JOIN ""Tags"" t_inner ON pt_inner.""TagId"" = t_inner.""TagId"" WHERE t_inner.""Name"" = ANY(@Tags))");
                parameters.Add("Tags", request.Tags);
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

            return await connection.QueryAsync<PostViewDto>(sqlBuilder.ToString(), parameters);
        }


        public async Task<IEnumerable<PostViewDto>> GetPendingPostsAsync(GetPendingPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
SELECT
    p.""PostId"", p.""AuthorId"", p.""Title"", p.""Summary"", p.""CreatedAt"",
    c.""Name"" AS CategoryName
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
                whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword)");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));
            sqlBuilder.AppendLine(@"ORDER BY p.""CreatedAt"" DESC");
            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Limit);
            parameters.Add("Offset", request.Offset);

            return await connection.QueryAsync<PostViewDto>(sqlBuilder.ToString(), parameters);
        }

        public async Task<IEnumerable<PostViewDto>> GetArchivedPostsAsync(GetArchivedPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
SELECT
    p.""PostId"", p.""AuthorId"", p.""Title"", p.""Summary"", p.""CreatedAt"", p.""Status"", p.""IsDeleted"",
    c.""Name"" AS CategoryName
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
                whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword)");
                parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%");
            }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));
            sqlBuilder.AppendLine(@"ORDER BY p.""UpdatedAt"" DESC, p.""DeletedAt"" DESC");
            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Limit);
            parameters.Add("Offset", request.Offset);

            return await connection.QueryAsync<PostViewDto>(sqlBuilder.ToString(), parameters);
        }

        public async Task<IEnumerable<PostViewDto>> GetMyPostsAsync(GetMyPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT
                    p.""PostId"", p.""AuthorId"", p.""Title"", p.""Summary"", p.""Content"", p.""Status"",
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

            return await connection.QueryAsync<PostViewDto>(sqlBuilder.ToString(), parameters);
        }

        public async Task<(PostViewDetailDto? Post, IEnumerable<CommentDto> Comments)> GetPostDetailsByIdAsync(Guid postId)
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sql = @"
                -- Query 1: Get Post Details
                SELECT
                    p.""PostId"", p.""AuthorId"", p.""Title"", p.""Summary"", p.""Content"", p.""PostType"",
                    p.""ViewsCount"", p.""CommentCount"", p.""UpvoteCount"", p.""DownvoteCount"", p.""IsFeatured"", p.""CreatedAt"",
                    c.""Name"" AS CategoryName
                FROM ""Posts"" p
                LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId""
                WHERE p.""PostId"" = @PostId AND p.""Status"" = 'Published' AND p.""IsDeleted"" = FALSE;

                -- Query 2: Get Tags
                SELECT t.""Name""
                FROM ""Tags"" t
                JOIN ""PostTags"" pt ON t.""TagId"" = pt.""TagId""
                WHERE pt.""PostId"" = @PostId;

                -- Query 3: Get Attachment URLs
                SELECT a.""Url""
                FROM ""Attachments"" a
                WHERE a.""TargetId"" = @PostId AND a.""TargetType"" = 'Post';
                
                -- Query 4: Get All Comments for the Post (flat list)
                SELECT 
                    c.""CommentId"", c.""AuthorId"", c.""ParentCommentId"", c.""Content"", 
                    c.""UpvoteCount"", c.""DownvoteCount"", c.""CreatedAt""
                FROM ""Comments"" c
                WHERE c.""PostId"" = @PostId AND c.""IsDeleted"" = FALSE
                ORDER BY c.""CreatedAt"" ASC;
            ";

            using (var multi = await connection.QueryMultipleAsync(sql, new { PostId = postId }))
            {
                var post = await multi.ReadSingleOrDefaultAsync<PostViewDetailDto>();
                if (post == null)
                {
                    return (null, Enumerable.Empty<CommentDto>());
                }

                post.Tags = (await multi.ReadAsync<string>()).ToList();
                post.Url = (await multi.ReadAsync<string>()).ToList();

                var comments = await multi.ReadAsync<CommentDto>();

                return (post, comments);
            }
        }
    }
}

