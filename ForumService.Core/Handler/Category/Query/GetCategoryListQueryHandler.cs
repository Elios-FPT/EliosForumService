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
    public class GetCategoryListQueryHandler : IQueryHandler<GetCategoryListQuery, PagedResponseDto<IEnumerable<CategoryDto>>>
    {
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private readonly IMapper _mapper;

        public GetCategoryListQueryHandler(IGenericRepository<Domain.Models.Category> categoryRepository, IMapper mapper)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<PagedResponseDto<IEnumerable<CategoryDto>>> Handle(GetCategoryListQuery request, CancellationToken cancellationToken)
        {
            // 1. Validate Pagination
            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.Size <= 0 ? 10 : request.Size;

            try
            {
                // 2. Build filter dynamically
                Expression<Func<Domain.Models.Category, bool>> filter = c => true;

                if (!string.IsNullOrEmpty(request.SearchKeyword))
                {
                    var searchKeywordSlug = GenerateSlug(request.SearchKeyword);
                    var searchLower = request.SearchKeyword.ToLower();

                    if (request.IsActive.HasValue)
                    {
                        // Case: Both Search and IsActive
                        var isActive = request.IsActive.Value;
                        filter = c => (c.Slug.Contains(searchKeywordSlug) || c.Name.ToLower().Contains(searchLower))
                                      && c.IsActive == isActive;
                    }
                    else
                    {
                        // Case: Search only
                        filter = c => c.Slug.Contains(searchKeywordSlug) || c.Name.ToLower().Contains(searchLower);
                    }
                }
                else if (request.IsActive.HasValue)
                {
                    // Case: IsActive only
                    var isActive = request.IsActive.Value;
                    filter = c => c.IsActive == isActive;
                }

                // 3. Get Total Count 
                var totalItems = await _categoryRepository.GetCountAsync(filter);

                if (totalItems == 0)
                {
                    return new PagedResponseDto<IEnumerable<CategoryDto>>(
                        Enumerable.Empty<CategoryDto>(), page, pageSize, 0)
                    {
                        Message = "No categories found."
                    };
                }

                // 4. Get Paged Data
                var categories = await _categoryRepository.GetListAsyncUntracked(
                    filter: filter,
                    orderBy: q => q.OrderBy(c => c.Name),
                    selector: x => x, 
                    pageSize: pageSize,
                    pageNumber: page
                );

                var result = _mapper.Map<IEnumerable<CategoryDto>>(categories);

                return new PagedResponseDto<IEnumerable<CategoryDto>>(
                    result,
                    page,
                    pageSize,
                    totalItems
                )
                {
                    Message = "Categories retrieved successfully."
                };
            }
            catch (Exception ex)
            {
                return new PagedResponseDto<IEnumerable<CategoryDto>>
                {
                    Status = 500,
                    Message = $"Failed to retrieve categories: {ex.Message}",
                    ResponseData = null,
                    Pagination = null
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
