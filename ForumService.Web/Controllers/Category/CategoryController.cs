using Asp.Versioning;
using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using static ForumService.Contract.UseCases.Category.Command;
using static ForumService.Contract.UseCases.Category.Query;
using static ForumService.Contract.UseCases.Category.Request;
using ForumService.Contract.TransferObjects.Category;

namespace ForumService.Web.Controllers.Category
{
    /// <summary>
    /// Category management endpoints.
    /// </summary>
    [ApiVersion(1)]
    [Produces("application/json")]
    [ControllerName("Category")]
    [Route("api/v1/[controller]")]
    public class CategoryController : ControllerBase
    {
        protected readonly ISender Sender;

        public CategoryController(ISender sender)
        {
            Sender = sender;
        }

        /// <summary>
        /// Creates a new category.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<BaseResponseDto<bool>> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var command = new CreateCategoryCommand(
                Name: request.Name,
                Description: request.Description,
                IsActive: request.IsActive
            );

                return await Sender.Send(command);
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to create category: {ex.Message}",
                    ResponseData = false
                };
            }
        }

        /// <summary>
        /// Updates an existing category.
        /// </summary>
        [HttpPut("{categoryId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> UpdateCategory([FromRoute] Guid categoryId, [FromBody] UpdateCategoryRequest request)
        {
            var command = new UpdateCategoryCommand(
                CategoryId: categoryId,
                Name: request.Name,
                Description: request.Description,
                IsActive: request.IsActive
            );

            return await Sender.Send(command);
        }

        /// <summary>
        /// Deletes a category by its ID.
        /// </summary>
        [HttpDelete("{CategoryId}")]
        [ProducesResponseType(typeof(BaseResponseDto<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<bool>> DeleteCategory([FromRoute] DeleteCategoryRequest request)
        {
            var command = new DeleteCategoryCommand(CategoryId: request.CategoryId);
            return await Sender.Send(command);
        }

        /// <summary>
        /// Retrieves all categories with optional filters.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(BaseResponseDto<IEnumerable<CategoryDto>>), StatusCodes.Status200OK)]
        public async Task<BaseResponseDto<IEnumerable<CategoryDto>>> GetCategories([FromQuery] GetCategoryListQuery request)
        {
            var query = new GetCategoryListQuery(
                SearchKeyword: request.SearchKeyword,
                Limit: request.Limit,
                Offset: request.Offset,
                IsActive: request.IsActive);
            return await Sender.Send(query);
        }

        /// <summary>
        /// Retrieves a category by its ID.
        /// </summary>
        [HttpGet("{CategoryId}")]
        [ProducesResponseType(typeof(BaseResponseDto<CategoryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<BaseResponseDto<CategoryDto>> GetCategoryById([FromRoute] GetCategoryByIdRequest request)
        {
            var query = new GetCategoryByIdQuery(CategoryId: request.CategoryId);
            return await Sender.Send(query);
        }
    }
}
