using MediatR;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ForumService.Contract.UseCases.Notification
{
    public static class Command
    {
        public record CreateNotificationCommand(
            Guid UserId,
            string Title,
            string Message,
            string? Url,
            string? Metadata) : ICommand<BaseResponseDto<bool>>;

        public record DeleteNotificationCommand(
            Guid NotificationId) : ICommand<BaseResponseDto<bool>>;

        public record MarkAsReadCommand(
            Guid NotificationId) : ICommand<BaseResponseDto<bool>>;

        public record MarkAllAsReadCommand(
            Guid UserId) : ICommand<BaseResponseDto<bool>>;

        public record ClearNotificationsCommand(
            Guid UserId) : ICommand<BaseResponseDto<bool>>;
    }
}
