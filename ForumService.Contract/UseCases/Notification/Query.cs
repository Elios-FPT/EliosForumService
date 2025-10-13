using MediatR;
using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.Notification
{
    public static class Query
    {
        public record GetNotificationsQuery(
            Guid UserId,
            bool UnreadOnly,
            int Limit,
            int Offset) : IQuery<BaseResponseDto<IEnumerable<NotificationDto>>>;

        public record GetUnreadCountQuery(
            Guid UserId) : IQuery<BaseResponseDto<int>>;
    }
}
