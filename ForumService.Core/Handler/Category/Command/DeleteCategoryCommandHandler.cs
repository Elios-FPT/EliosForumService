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
    public class DeleteCategoryCommandHandler : ICommandHandler<DeleteCategoryCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<Domain.Models.Category> _categoryRepository;
        private readonly IGenericRepository<Domain.Models.Post> _postRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteCategoryCommandHandler(
            IGenericRepository<Domain.Models.Category> categoryRepository,
             IGenericRepository<Domain.Models.Post> postRepository,
            IUnitOfWork unitOfWork)
        {
            _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
            _postRepository = postRepository ?? throw new ArgumentNullException(nameof(postRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<bool>> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
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

            // Kiểm tra các bài viết có IsDeleted = false thuộc thể loại này
            var activePostsCount = await _postRepository.GetCountAsync(p =>
                p.CategoryId == request.CategoryId && !p.IsDeleted);

            if (activePostsCount > 0)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = $"Cannot delete category because it is being used by {activePostsCount} active post(s).",
                    ResponseData = false
                };
            }

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                await _categoryRepository.DeleteAsync(category);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Category deleted successfully.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Failed to delete category: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}
