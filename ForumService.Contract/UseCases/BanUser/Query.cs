using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.BanUser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Contract.UseCases.BanUser
{
    public static partial class Query
    {
        /// <summary>
        /// Query to get list of bans.
        /// </summary>
        public record GetBannedUsersQuery(
             Guid? UserId,
             bool? IsActive,
             int Page,
             int Size
         ) : IQuery<PagedResponseDto<IEnumerable<BanDto>>>;

        /// <summary>
        /// Query details of a ban ID.
        /// /// </summary>
        public record GetBanByIdQuery(Guid BanId) : IQuery<BaseResponseDto<BanDto>>;

        /// <summary>
        /// Query to get the ban status of the current user.
        /// Returns UserBanStatusDto which contains simplified ban info.
        /// </summary>
        public record GetMyBanStatusQuery(Guid UserId) : IQuery<BaseResponseDto<UserBanStatusDto>>;
    }
}
