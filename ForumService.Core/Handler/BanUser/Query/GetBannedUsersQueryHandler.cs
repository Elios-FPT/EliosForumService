using ForumService.Contract.Message;
using ForumService.Contract.Models;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.BanUser;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.BanUser.Query;

namespace ForumService.Core.Handler.BanUser.Query
{
    public class GetBannedUsersQueryHandler : IQueryHandler<GetBannedUsersQuery, PagedResponseDto<IEnumerable<BanDto>>>
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

        public async Task<PagedResponseDto<IEnumerable<BanDto>>> Handle(GetBannedUsersQuery request, CancellationToken cancellationToken)
        {
            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.Size <= 0 ? 20 : request.Size;

            try
            {
                // 1. Build Filter Expression
                Expression<Func<ForumUserBan, bool>> filter = b => true;

                if (request.UserId.HasValue && request.IsActive.HasValue)
                {
                    var uid = request.UserId.Value;
                    var active = request.IsActive.Value;
                    filter = b => b.UserId == uid && b.IsActive == active;
                }
                else if (request.UserId.HasValue)
                {
                    var uid = request.UserId.Value;
                    filter = b => b.UserId == uid;
                }
                else if (request.IsActive.HasValue)
                {
                    var active = request.IsActive.Value;
                    filter = b => b.IsActive == active;
                }

                // 2. Get Total Count
                var totalItems = await _banRepository.GetCountAsync(filter);

                if (totalItems == 0)
                {
                    return new PagedResponseDto<IEnumerable<BanDto>>(
                        Enumerable.Empty<BanDto>(), page, pageSize, 0)
                    {
                        Message = "No banned users found."
                    };
                }

                // 3. Get Paged Data from DB
                var bans = await _banRepository.GetListAsync(
                    filter: filter,
                    orderBy: q => q.OrderByDescending(b => b.BannedAt),
                    pageSize: pageSize,
                    pageNumber: page
                );

                // 4. Fetch User Profiles
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

                // 5. Map Entity -> DTO
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

                return new PagedResponseDto<IEnumerable<BanDto>>(
                    banDtos,
                    page,
                    pageSize,
                    totalItems
                )
                {
                    Message = "Get list successfully."
                };
            }
            catch (Exception ex)
            {
                return new PagedResponseDto<IEnumerable<BanDto>>
                {
                    Status = 500,
                    Message = $"Query error: {ex.Message}",
                    ResponseData = Enumerable.Empty<BanDto>(),
                    Pagination = null
                };
            }
        }
    }
}
