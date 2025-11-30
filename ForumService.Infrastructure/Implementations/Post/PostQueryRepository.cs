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

        public async Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetPublicViewPostsAsync(GetPublicViewPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);

            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            // Using the Dapper Multi-Map optimization from the previous answer
            sqlBuilder.AppendLine(@"
                SELECT
                    COUNT(*) OVER() as TotalCount,
                    p.*, 
                    c.""CategoryId"", c.""Name"", c.""Description""
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

            if (request.ReferenceId.HasValue)
            {
                whereClauses.Add(@"p.""ReferenceId"" = @ReferenceId");
                parameters.Add("ReferenceId", request.ReferenceId.Value);
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

            var pageIndex = request.Page <= 0 ? 0 : request.Page - 1;
            var offset = pageIndex * request.Size;

            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Size);
            parameters.Add("Offset", offset);

            int totalCount = 0;

            // Using Dapper Multi-mapping
            var posts = await connection.QueryAsync<long, Domain.Models.Post, Domain.Models.Category, Domain.Models.Post>(
               sqlBuilder.ToString(),
               (count, post, category) =>
               {
                   totalCount = (int)count; 
                   post.Category = category;
                   return post;
               },
               parameters,
               splitOn: "PostId,CategoryId"
           );

            return (posts.DistinctBy(p => p.PostId), totalCount);
        }

        // --- 2. GetModeratorPublicViewPostsAsync ---
        public async Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetModeratorPublicViewPostsAsync(GetModeratorPublicPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT COUNT(*) OVER() as TotalCount, p.*, c.""CategoryId"", c.""Name"", c.""Description""
                FROM ""Posts"" p LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId"" ");

            var whereClauses = new List<string> { @"p.""Status"" = 'Published'", @"p.""IsDeleted"" = FALSE" };

            // Filters
            if (request.AuthorId.HasValue) { whereClauses.Add(@"p.""AuthorId"" = @AuthorId"); parameters.Add("AuthorId", request.AuthorId.Value); }
            if (request.CategoryId.HasValue) { whereClauses.Add(@"p.""CategoryId"" = @CategoryId"); parameters.Add("CategoryId", request.CategoryId.Value); }
            if (!string.IsNullOrWhiteSpace(request.PostType)) { whereClauses.Add(@"p.""PostType"" = @PostType"); parameters.Add("PostType", request.PostType); }
            if (!string.IsNullOrWhiteSpace(request.SearchKeyword)) { whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword OR p.""Summary"" ILIKE @SearchKeyword)"); parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%"); }
            if (request.ReferenceId.HasValue) { whereClauses.Add(@"p.""ReferenceId"" = @ReferenceId"); parameters.Add("ReferenceId", request.ReferenceId.Value); }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch { "viewscount" => @"p.""ViewsCount""", "upvotecount" => @"p.""UpvoteCount""", _ => @"p.""CreatedAt""" };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            // Pagination
            var pageIndex = request.Page <= 0 ? 0 : request.Page - 1;
            var offset = pageIndex * request.Size;
            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Size);
            parameters.Add("Offset", offset);

            int totalCount = 0;
            var posts = await connection.QueryAsync<long, Domain.Models.Post, Domain.Models.Category, Domain.Models.Post>(
                sqlBuilder.ToString(), (count, post, category) => { totalCount = (int)count; post.Category = category; return post; },
                parameters, splitOn: "PostId,CategoryId");

            return (posts.DistinctBy(p => p.PostId), totalCount);
        }

        // --- 3. GetPendingPostsAsync ---
        public async Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetPendingPostsAsync(GetPendingPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT COUNT(*) OVER() as TotalCount, p.*, c.""CategoryId"", c.""Name"", c.""Description""
                FROM ""Posts"" p LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId"" ");

            var whereClauses = new List<string> { @"p.""Status"" = 'PendingReview'", @"p.""IsDeleted"" = FALSE" };

            // Filters
            if (request.AuthorId.HasValue) { whereClauses.Add(@"p.""AuthorId"" = @AuthorId"); parameters.Add("AuthorId", request.AuthorId.Value); }
            if (request.CategoryId.HasValue) { whereClauses.Add(@"p.""CategoryId"" = @CategoryId"); parameters.Add("CategoryId", request.CategoryId.Value); }
            if (!string.IsNullOrWhiteSpace(request.PostType)) { whereClauses.Add(@"p.""PostType"" = @PostType"); parameters.Add("PostType", request.PostType); }
            if (!string.IsNullOrWhiteSpace(request.SearchKeyword)) { whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword OR p.""Summary"" ILIKE @SearchKeyword)"); parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%"); }
            if (request.ReferenceId.HasValue) { whereClauses.Add(@"p.""ReferenceId"" = @ReferenceId"); parameters.Add("ReferenceId", request.ReferenceId.Value); }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch { "viewscount" => @"p.""ViewsCount""", "upvotecount" => @"p.""UpvoteCount""", _ => @"p.""CreatedAt""" };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            // Pagination
            var pageIndex = request.Page <= 0 ? 0 : request.Page - 1;
            var offset = pageIndex * request.Size;
            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Size);
            parameters.Add("Offset", offset);

            int totalCount = 0;
            var posts = await connection.QueryAsync<long, Domain.Models.Post, Domain.Models.Category, Domain.Models.Post>(
                sqlBuilder.ToString(), (count, post, category) => { totalCount = (int)count; post.Category = category; return post; },
                parameters, splitOn: "PostId,CategoryId");

            return (posts.DistinctBy(p => p.PostId), totalCount);
        }

        // --- 4. GetArchivedPostsAsync ---
        public async Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetArchivedPostsAsync(GetArchivedPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            sqlBuilder.AppendLine(@"
                SELECT COUNT(*) OVER() as TotalCount, p.*, c.""CategoryId"", c.""Name"", c.""Description""
                FROM ""Posts"" p LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId"" ");

            var whereClauses = new List<string> { @"(p.""Status"" = 'Rejected' OR p.""IsDeleted"" = TRUE)" };

            // Filters
            if (request.AuthorId.HasValue) { whereClauses.Add(@"p.""AuthorId"" = @AuthorId"); parameters.Add("AuthorId", request.AuthorId.Value); }
            if (request.CategoryId.HasValue) { whereClauses.Add(@"p.""CategoryId"" = @CategoryId"); parameters.Add("CategoryId", request.CategoryId.Value); }
            if (!string.IsNullOrWhiteSpace(request.PostType)) { whereClauses.Add(@"p.""PostType"" = @PostType"); parameters.Add("PostType", request.PostType); }
            if (!string.IsNullOrWhiteSpace(request.SearchKeyword)) { whereClauses.Add(@"(p.""Title"" ILIKE @SearchKeyword OR p.""Summary"" ILIKE @SearchKeyword)"); parameters.Add("SearchKeyword", $"%{request.SearchKeyword}%"); }
            if (request.ReferenceId.HasValue) { whereClauses.Add(@"p.""ReferenceId"" = @ReferenceId"); parameters.Add("ReferenceId", request.ReferenceId.Value); }

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch { "updatedat" => @"p.""UpdatedAt""", "deletedat" => @"p.""DeletedAt""", "createdat" => @"p.""CreatedAt""", _ => @"p.""UpdatedAt""" };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            // Pagination
            var pageIndex = request.Page <= 0 ? 0 : request.Page - 1;
            var offset = pageIndex * request.Size;
            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Size);
            parameters.Add("Offset", offset);

            int totalCount = 0;
            var posts = await connection.QueryAsync<long, Domain.Models.Post, Domain.Models.Category, Domain.Models.Post>(
                sqlBuilder.ToString(), (count, post, category) => { totalCount = (int)count; post.Category = category; return post; },
                parameters, splitOn: "PostId,CategoryId");

            return (posts.DistinctBy(p => p.PostId), totalCount);
        }

        // --- 5. GetMyPostsAsync ---
        public async Task<(IEnumerable<Domain.Models.Post> Posts, int TotalCount)> GetMyPostsAsync(GetMyPostsQuery request)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();

            // Thêm COUNT(*) OVER() and map Category
            sqlBuilder.AppendLine(@"
                SELECT
                    COUNT(*) OVER() as TotalCount,
                    p.*, 
                    c.""CategoryId"", c.""Name"", c.""Description""
                FROM ""Posts"" p
                LEFT JOIN ""Categories"" c ON p.""CategoryId"" = c.""CategoryId""
                ");

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

            sqlBuilder.Append("WHERE ").AppendLine(string.Join(" AND ", whereClauses));

            var sortBy = request.SortBy?.ToLower() switch
            {
                "viewscount" => @"p.""ViewsCount""",
                "createdat" => @"p.""CreatedAt""",
                _ => @"p.""CreatedAt"""
            };
            var sortOrder = request.SortOrder?.ToUpper() == "ASC" ? "ASC" : "DESC";
            sqlBuilder.AppendLine($"ORDER BY {sortBy} {sortOrder}");

            // Pagination Calculation
            var pageIndex = request.Page <= 0 ? 0 : request.Page - 1;
            var offset = pageIndex * request.Size;

            sqlBuilder.AppendLine("LIMIT @Limit OFFSET @Offset");
            parameters.Add("Limit", request.Size);
            parameters.Add("Offset", offset);

            int totalCount = 0;

            // Mapping: <long, Post, Category, Post> để xử lý TotalCount
            var posts = await connection.QueryAsync<long, Domain.Models.Post, Domain.Models.Category, Domain.Models.Post>(
                sqlBuilder.ToString(),
                (count, post, category) =>
                {
                    totalCount = (int)count;
                    post.Category = category;
                    return post;
                },
                parameters,
                splitOn: "PostId,CategoryId"
            );

            return (posts.DistinctBy(p => p.PostId), totalCount);
        }
    }
}

