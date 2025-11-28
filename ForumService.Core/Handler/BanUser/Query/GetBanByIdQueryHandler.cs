using ForumService.Contract.Message;
using ForumService.Contract.Models;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.BanUser;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Handler.BanUser.Query
{
    public class GetBanByIdQueryHandler : IQueryHandler<Contract.UseCases.BanUser.Query.GetBanByIdQuery, BaseResponseDto<BanDto>>
    {
        private readonly IGenericRepository<ForumUserBan> _banRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository;
        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetBanByIdQueryHandler(
            IGenericRepository<ForumUserBan> banRepository,
            IKafkaProducerRepository<User> producerRepository)
        {
            _banRepository = banRepository ?? throw new ArgumentNullException(nameof(banRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
        }

        public async Task<BaseResponseDto<BanDto>> Handle(Contract.UseCases.BanUser.Query.GetBanByIdQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var ban = await _banRepository.GetByIdAsync(request.BanId);
                if (ban == null)
                {
                    return new BaseResponseDto<BanDto> { Status = 404, Message = "Không tìm thấy lệnh cấm.", ResponseData = null };
                }

                // Hydrate User Info
                User? bannedUser = null;
                User? bannerAdmin = null;
                User? unbannerAdmin = null;

                try
                {
                    var userProfilesList = await _producerRepository.ProduceGetAllAsync(DestinationService, ResponseTopic);

                    var userDict = userProfilesList.ToDictionary(u => u.id);

                    userDict.TryGetValue(ban.UserId, out bannedUser);
                    userDict.TryGetValue(ban.BannedBy, out bannerAdmin);
                    if (ban.UnbannedBy.HasValue)
                    {
                        userDict.TryGetValue(ban.UnbannedBy.Value, out unbannerAdmin);
                    }
                }
                catch
                {
                    // Ignore error fetching user details
                }

                var banDto = new BanDto
                {
                    Id = ban.Id,
                    UserId = ban.UserId,
                    UserFirstName = bannedUser?.firstName,
                    UserLastName = bannedUser?.lastName,
                    UserAvatarUrl = bannedUser?.avatarUrl,

                    Reason = ban.Reason,

                    BannedBy = ban.BannedBy,
                    BannedByFirstName = bannerAdmin?.firstName,
                    BannedByLastName = bannerAdmin?.lastName,

                    BannedAt = ban.BannedAt,
                    BanUntil = ban.BanUntil,
                    IsActive = ban.IsActive,
                    UnbannedAt = ban.UnbannedAt,
                    UnbannedBy = ban.UnbannedBy,
                    UnbanReason = ban.UnbanReason
                };

                return new BaseResponseDto<BanDto> { Status = 200, Message = "Thành công.", ResponseData = banDto };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<BanDto> { Status = 500, Message = ex.Message, ResponseData = null };
            }
        }
    }
}
