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
    public class UpdateCategoryCommandHandler : ICommandHandler<UpdateCategoryCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateCategoryCommandHandler(
            IGenericRepository<Domain.Models.Category> categoryRepository,
            IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<BaseResponseDto<bool>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
            if (category == null)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 404,
                    Message = "Category not found.",
                    ResponseData = false
                };
            }

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                category.Name = request.Name ?? category.Name;
                category.Slug = GenerateSlug(request.Name) ?? category.Slug;
                category.Description = request.Description ?? category.Description;
                category.IsActive = request.IsActive;
                category.UpdatedAt = DateTime.UtcNow;

                await _categoryRepository.UpdateAsync(category);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Category updated successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to update category: {ex.Message}",
                    ResponseData = false
                };
            }
        }

        private string GenerateSlug(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Chuẩn hóa và loại bỏ dấu tiếng Việt
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

            // Kết quả sau khi bỏ dấu
            var slug = builder.ToString().Normalize(NormalizationForm.FormC)
                .ToLower()
                .Replace("đ", "d") // đặc biệt cho tiếng Việt
                .Replace(" ", "-");

            // Loại bỏ ký tự đặc biệt
            slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

            // Loại bỏ dấu '-' thừa
            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");

            return slug.Trim('-');
        }
    }
}