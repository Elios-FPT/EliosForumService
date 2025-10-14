using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Category
{
    public static class Command
    {
        /// <summary>
        /// Command để tạo mới Category.
        /// </summary>
        public record CreateCategoryCommand(
            string Name,
            string? Description,
            bool IsActive = true
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command để cập nhật Category.
        /// </summary>
        public record UpdateCategoryCommand(
            Guid CategoryId,
            string Name,
            string? Description,
            bool IsActive = true
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command để xóa Category.
        /// </summary>
        public record DeleteCategoryCommand(
            Guid CategoryId
        ) : ICommand<BaseResponseDto<bool>>;
    }
}
