using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Category
{
    public static class Request
    {
        /// <summary>
        /// Request để tạo mới Category.
        /// </summary>
        public record CreateCategoryRequest(
            [Required, MaxLength(100)] string Name,
            string? Description,
            bool IsActive = true
        );

        /// <summary>
        /// Request để cập nhật Category.
        /// </summary>
        public record UpdateCategoryRequest(
            Guid CategoryId,
            [Required, MaxLength(100)] string Name,
            string? Description,
            bool IsActive = true
        );

        /// <summary>
        /// Request để xóa Category theo ID.
        /// </summary>
        public record DeleteCategoryRequest(
            Guid CategoryId
        );

        /// <summary>
        /// Request để lấy thông tin chi tiết Category theo ID.
        /// </summary>
        public record GetCategoryByIdRequest(
            Guid CategoryId
        );

        /// <summary>
        /// Request để lấy danh sách Category có phân trang.
        /// </summary>
        public record GetCategoriesRequest(
            string? SearchKeyword = null,
            bool? IsActive = null,
            int Limit = 20,
            int Offset = 0
        );
    }
}
