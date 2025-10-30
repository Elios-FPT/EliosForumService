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
       
        public record CreateCategoryRequest(
            [Required, MaxLength(100)] string Name,
            string? Description,
            bool IsActive = true
        );

      
        public record UpdateCategoryRequest(
            Guid CategoryId,
            [Required, MaxLength(100)] string Name,
            string? Description,
            bool IsActive = true
        );

       
        public record DeleteCategoryRequest(
            Guid CategoryId
        );

        public record GetCategoryByIdRequest(
            Guid CategoryId
        );

        public record GetCategoriesRequest(
            string? SearchKeyword = null,
            bool? IsActive = null,
            int Limit = 20,
            int Offset = 0
        );
    }
}
