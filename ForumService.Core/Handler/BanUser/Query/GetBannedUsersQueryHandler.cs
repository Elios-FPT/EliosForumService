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
    public class GetBannedUsersQueryHandler : IQueryHandler<Contract.UseCases.BanUser.Query.GetBannedUsersQuery, BaseResponseDto<IEnumerable<BanDto>>>
    {
        private readonly IGenericRepository<ForumUserBan> _banRepository;
        private readonly IKafkaProducerRepository<User> _producerRepository; 

        private const string ResponseTopic = "user-forum-user";
        private const string DestinationService = "user";

        public GetBannedUsersQueryHandler(
            IGenericRepository<ForumUserBan> banRepository,
            IKafkaProducerRepository<User> producerRepository)
        {
            _banRepository = banRepository ?? throw new ArgumentNullException(nameof(banRepository));
            _producerRepository = producerRepository ?? throw new ArgumentNullException(nameof(producerRepository));
        }

        public async Task<BaseResponseDto<IEnumerable<BanDto>>> Handle(Contract.UseCases.BanUser.Query.GetBannedUsersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                // 1. Build Filter & Get Data from DB
                System.Linq.Expressions.Expression<Func<ForumUserBan, bool>> filter = b => true;

                if (request.UserId.HasValue)
                {
                    var oldFilter = filter;
                    filter = b => oldFilter.Compile()(b) && b.UserId == request.UserId.Value;
                }

                if (request.IsActive.HasValue)
                {
                    var oldFilter = filter;
                    filter = b => oldFilter.Compile()(b) && b.IsActive == request.IsActive.Value;
                }

                int limit = request.Limit > 0 ? request.Limit : 20;
                int pageNumber = (request.Offset / limit) + 1;

                var bans = await _banRepository.GetListAsync(
                    filter: filter,
                    orderBy: q => q.OrderByDescending(b => b.BannedAt),
                    pageSize: limit,
                    pageNumber: pageNumber
                );

                if (!bans.Any())
                {
                    return new BaseResponseDto<IEnumerable<BanDto>>
                    {
                        Status = 200,
                        Message = "Không có dữ liệu.",
                        ResponseData = Enumerable.Empty<BanDto>()
                    };
                }

                var userIdsToFetch = bans.Select(b => b.UserId)
                                         .Concat(bans.Select(b => b.BannedBy))
                                         .Distinct()
                                         .ToList();

                Dictionary<Guid, User> userProfilesDict;
                try
                {
                    var userProfilesList = await _producerRepository.ProduceGetAllAsync(
                           DestinationService,
                           ResponseTopic);

                    userProfilesDict = userProfilesList
                        .Where(u => userIdsToFetch.Contains(u.id))
                        .ToDictionary(u => u.id);
                }
                catch
                {
                    userProfilesDict = new Dictionary<Guid, User>();
                }

                // 3. Map Entity -> DTO
                var banDtos = bans.Select(b =>
                {
                    userProfilesDict.TryGetValue(b.UserId, out var bannedUser);
                    userProfilesDict.TryGetValue(b.BannedBy, out var bannerAdmin);

                    return new BanDto
                    {
                        Id = b.Id,
                        UserId = b.UserId,
                        UserFirstName = bannedUser?.firstName,
                        UserLastName = bannedUser?.lastName,
                        UserAvatarUrl = bannedUser?.avatarUrl,

                        Reason = b.Reason,

                        BannedBy = b.BannedBy,
                        BannedByFirstName = bannerAdmin?.firstName,
                        BannedByLastName = bannerAdmin?.lastName,

                        BannedAt = b.BannedAt,
                        BanUntil = b.BanUntil,
                        IsActive = b.IsActive,
                        UnbannedAt = b.UnbannedAt,
                        UnbannedBy = b.UnbannedBy,
                        UnbanReason = b.UnbanReason
                    };
                });

                return new BaseResponseDto<IEnumerable<BanDto>>
                {
                    Status = 200,
                    Message = "Lấy danh sách thành công.",
                    ResponseData = banDtos
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<IEnumerable<BanDto>>
                {
                    Status = 500,
                    Message = $"Lỗi truy vấn: {ex.Message}",
                    ResponseData = Enumerable.Empty<BanDto>()
                };
            }
        }
    }
}
