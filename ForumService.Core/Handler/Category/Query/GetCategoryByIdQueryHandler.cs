using AutoMapper;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Query;

namespace ForumService.Core.Handler.Category.Query
{
    public class GetCategoryByIdQueryHandler : IQueryHandler<GetCategoryByIdQuery, BaseResponseDto<CategoryDto>>
    {
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private readonly IMapper _mapper;

        public GetCategoryByIdQueryHandler(
            IGenericRepository<Domain.Models.Category> categoryRepository,
            IMapper mapper)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<BaseResponseDto<CategoryDto>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
               
                if (request.CategoryId == Guid.Empty)
                {
                    return new BaseResponseDto<CategoryDto>
                    {
                        Status = 400,
                        Message = "Invalid CategoryId.",
                        ResponseData = null
                    };
                }

               
                var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
                if (category == null)
                {
                    return new BaseResponseDto<CategoryDto>
                    {
                        Status = 404,
                        Message = "Category not found.",
                        ResponseData = null
                    };
                }

               
                var categoryDto = _mapper.Map<CategoryDto>(category);

                return new BaseResponseDto<CategoryDto>
                {
                    Status = 200,
                    Message = "Category retrieved successfully.",
                    ResponseData = categoryDto
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<CategoryDto>
                {
                    Status = 500,
                    Message = $"Failed to retrieve category: {ex.Message}",
                    ResponseData = null
                };
            }
        }
    }
}
