using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Command;

namespace ForumService.Core.Handler.Category.Command
{
    public class CreateCategoryCommandHandler : ICommandHandler<CreateCategoryCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateCategoryCommandHandler(
            IGenericRepository<Domain.Models.Category> categoryRepository,
            IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Category name cannot be empty.",
                    ResponseData = false
                };
            }

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                var category = new Domain.Models.Category
                {
                    CategoryId = Guid.NewGuid(),
                    Name = request.Name,
                    Slug = GenerateSlug(request.Name),
                    Description = request.Description,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _categoryRepository.AddAsync(category);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Category created successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to create category: {ex.Message}",
                    ResponseData = false
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
