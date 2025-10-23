using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ForumService.Contract.TransferObjects.Category;

namespace ForumService.Contract.UseCases.Category
{
    public static class Query
    {
        /// <summary>
        /// Query to retrieve a list of categories (with pagination and filtering).
        /// </summary>
        public record GetCategoryListQuery(
            string? SearchKeyword = null,
            bool? IsActive = null,
            int Limit = 20,
            int Offset = 0
        ) : IQuery<BaseResponseDto<IEnumerable<CategoryDto>>>;

        /// <summary>
        /// Query to get category details by ID.
        /// </summary>
        public record GetCategoryByIdQuery(
            Guid CategoryId
        ) : IQuery<BaseResponseDto<CategoryDto>>;

        /// <summary>
        /// Query to retrieve all active categories.
        /// </summary>
        public record GetActiveCategoriesQuery()
            : IQuery<BaseResponseDto<IEnumerable<CategoryDto>>>;
    }
}
