using ForumService.Contract.Message;
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
    public class GetMyBanStatusQueryHandler : IQueryHandler<Contract.UseCases.BanUser.Query.GetMyBanStatusQuery, BaseResponseDto<UserBanStatusDto>>
    {
        private readonly IGenericRepository<ForumUserBan> _banRepository;

        public GetMyBanStatusQueryHandler(IGenericRepository<ForumUserBan> banRepository)
        {
            _banRepository = banRepository ?? throw new ArgumentNullException(nameof(banRepository));
        }

        public async Task<BaseResponseDto<UserBanStatusDto>> Handle(Contract.UseCases.BanUser.Query.GetMyBanStatusQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var allBans = await _banRepository.GetListAsync();

                var activeBan = allBans.FirstOrDefault(b =>
                    b.UserId == request.UserId &&
                    b.IsActive &&
                    (!b.BanUntil.HasValue || b.BanUntil.Value > DateTime.UtcNow)
                );

                UserBanStatusDto statusDto;

                if (activeBan != null)
                {
                    // User is Banned
                    statusDto = new UserBanStatusDto
                    {
                        IsBanned = true,
                        Reason = activeBan.Reason,
                        BanUntil = activeBan.BanUntil
                        // IsPermanent is calculated automatically in DTO
                    };
                }
                else
                {
                    // User is Active (Not banned)
                    statusDto = new UserBanStatusDto
                    {
                        IsBanned = false,
                        Reason = null,
                        BanUntil = null
                    };
                }

                return new BaseResponseDto<UserBanStatusDto>
                {
                    Status = 200,
                    Message = "Successfully retrieved ban status.",
                    ResponseData = statusDto
                };
            }
            catch (Exception ex)
            {
                return new BaseResponseDto<UserBanStatusDto>
                {
                    Status = 500,
                    Message = $"Error retrieving ban status: {ex.Message}",
                    ResponseData = null
                };
            }
        }
    }
}
