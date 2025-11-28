using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.BanUser
{
    public static partial class Command
    {
        /// <summary>
        /// Command to execute the ban logic.
        /// </summary>
        public record CreateBanUserCommand(
            Guid UserId,
            string Reason,
            Guid BannedBy,
            DateTime? BanUntil
        ) : ICommand<BaseResponseDto<Guid>>;

        /// <summary>
        /// Command unban.
        /// </summary>
        public record UnbanUserCommand(
            Guid BanId,
            Guid UnbannedBy,
            string? UnbanReason
        ) : ICommand<BaseResponseDto<bool>>;

        /// <summary>
        /// Command update ban.
        /// </summary>
        public record UpdateBanCommand(
            Guid BanId,
            Guid UpdatedBy, 
            string Reason,
            DateTime? BanUntil
        ) : ICommand<BaseResponseDto<bool>>;
    }
}
