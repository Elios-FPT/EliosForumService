using AutoMapper;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Query;

namespace ForumService.Core.Handler.Category.Query
{
    public class GetCategoryListQueryHandler : IQueryHandler<GetCategoryListQuery, BaseResponseDto<IEnumerable<CategoryDto>>>
    {
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private readonly IMapper _mapper;


        public GetCategoryListQueryHandler(IGenericRepository<Domain.Models.Category> categoryRepository, IMapper mapper)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<BaseResponseDto<IEnumerable<CategoryDto>>> Handle(GetCategoryListQuery request, CancellationToken cancellationToken)
        {
            // Validate limit and offset
            if (request.Limit <= 0 || request.Offset < 0)
            {
                return new BaseResponseDto<IEnumerable<CategoryDto>>
                {
                    Status = 400,
                    Message = "Limit must be positive and Offset must be non-negative.",
                    ResponseData = null
                };
            }

            try
            {
                // Build filter dynamically
                Expression<Func<Domain.Models.Category, bool>> filter = c => true;

                if (!string.IsNullOrEmpty(request.SearchKeyword))
                {
                    var searchKeywordSlug = GenerateSlug(request.SearchKeyword);
                    filter = c => c.Slug.Contains(searchKeywordSlug);
                }

                if (request.IsActive.HasValue)
                {
                    var isActive = request.IsActive.Value;
                    // Build expression dynamically
                    var param = Expression.Parameter(typeof(Domain.Models.Category), "c");
                    var body = Expression.AndAlso(
                        Expression.Invoke(filter, param),
                        Expression.Equal(
                            Expression.Property(param, nameof(Domain.Models.Category.IsActive)),
                            Expression.Constant(isActive)
                        )
                    );
                    filter = Expression.Lambda<Func<Domain.Models.Category, bool>>(body, param);
                }

                var categories = await _categoryRepository.GetListAsyncUntracked<Domain.Models.Category>(
                    filter: filter,
                    orderBy: q => q.OrderBy(c => c.Name),
                    pageSize: request.Limit,
                    pageNumber: request.Offset + 1
                );

                var result = _mapper.Map<IEnumerable<CategoryDto>>(categories);

                return new BaseResponseDto<IEnumerable<CategoryDto>>
                {
                    Status = 200,
                    Message = categories.Any() ? "Categories retrieved successfully." : "No categories found.",
                    ResponseData = result
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<CategoryDto>>
                {
                    Status = 500,
                    Message = $"Failed to retrieve categories: {ex.Message}",
                    ResponseData = null
                };
            }
        }

        private string GenerateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var normalized = name.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var c in normalized)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            var slug = builder.ToString().Normalize(NormalizationForm.FormC)
                .ToLower()
                .Replace("đ", "d")
                .Replace(" ", "-");

            slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");

            return slug.Trim('-');
        }
    }
}
