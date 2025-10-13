using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Notification
{
    public static class Request
    {
        public record CreateNotificationRequest(
            Guid UserId,
            string Title,
            string Message,
            string? Url,
            string? Metadata);

        public record GetNotificationsRequest(
            Guid UserId,
            bool UnreadOnly = false,
            int Limit = 20,
            int Offset = 0);

        public record DeleteNotificationRequest(
            Guid NotificationId);

        public record GetUnreadCountRequest(
            Guid UserId);

        public record MarkAsReadRequest(
            Guid NotificationId);

        public record MarkAllAsReadRequest(
            Guid UserId);

        public record ClearNotificationsRequest(
            Guid UserId);
    }
}
