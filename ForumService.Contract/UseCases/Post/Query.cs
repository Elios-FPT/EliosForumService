using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Post;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Post
{
    public static class Query
    {
        /// <summary>
        /// Query để lấy danh sách bài viết có phân trang và bộ lọc tùy chọn.
        /// </summary>
        public record GetPostsQuery(
            Guid? AuthorId = null,
            Guid? CategoryId = null,
            string? Status = null,
            string? SearchKeyword = null,
            int Limit = 20,
            int Offset = 0
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;

        /// <summary>
        /// Query để lấy danh sách bài viết công khai có phân trang, bộ lọc, và tùy chọn sắp xếp.
        /// </summary>
        public record GetPublicViewPostsQuery(
            Guid? AuthorId = null,
            Guid? CategoryId = null,
            string? SearchKeyword = null,
            int Limit = 20,
            int Offset = 0,
            // --- Các trường mới được thêm vào ---
            List<string>? Tags = null,
            string? SortBy = null,
            string? SortOrder = null
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;

        /// <summary>
        /// Query để lấy thông tin chi tiết của một bài viết theo ID.
        /// </summary>
        public record GetPostByIdQuery(
            Guid PostId
        ) : IQuery<BaseResponseDto<PostViewDto>>;

        /// <summary>
        /// Query để lấy tổng số bài viết của một tác giả.
        /// </summary>
        public record GetPostCountByAuthorQuery(
            Guid AuthorId
        ) : IQuery<BaseResponseDto<int>>;

        /// <summary>
        /// Query để lấy danh sách bài viết nổi bật (featured).
        /// </summary>
        public record GetFeaturedPostsQuery(
            int Limit = 10,
            int Offset = 0
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;

        /// <summary>
        /// Query để lấy danh sách bài viết theo category.
        /// </summary>
        public record GetPostsByCategoryQuery(
            Guid CategoryId,
            int Limit = 20,
            int Offset = 0
        ) : IQuery<BaseResponseDto<IEnumerable<PostViewDto>>>;
    }
}
