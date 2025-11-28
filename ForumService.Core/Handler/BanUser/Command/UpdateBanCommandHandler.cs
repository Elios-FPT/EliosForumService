using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Handler.BanUser.Command
{
    public class UpdateBanCommandHandler : ICommandHandler<Contract.UseCases.BanUser.Command.UpdateBanCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<ForumUserBan> _banRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateBanCommandHandler(IGenericRepository<ForumUserBan> banRepository, IUnitOfWork unitOfWork)
        {
            _banRepository = banRepository ?? throw new ArgumentNullException(nameof(banRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<BaseResponseDto<bool>> Handle(Contract.UseCases.BanUser.Command.UpdateBanCommand request, CancellationToken cancellationToken)
        {
            var ban = await _banRepository.GetByIdAsync(request.BanId);
            if (ban == null)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 404,
                    Message = "Không tìm thấy lệnh cấm.",
                    ResponseData = false
                };
            }

            if (request.BanUntil.HasValue && request.BanUntil.Value <= DateTime.UtcNow)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Thời hạn cấm mới phải lớn hơn thời điểm hiện tại.",
                    ResponseData = false
                };
            }

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                ban.Reason = request.Reason;
                ban.BanUntil = request.BanUntil;
 

                if (!ban.IsActive && (ban.BanUntil == null || ban.BanUntil > DateTime.UtcNow))
                {
                    ban.IsActive = true;
                    ban.UnbannedAt = null;
                    ban.UnbannedBy = null;
                    ban.UnbanReason = null;
                }

                await _banRepository.UpdateAsync(ban);
                await _unitOfWork.CommitAsync();

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Cập nhật thông tin lệnh cấm thành công.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Lỗi cập nhật: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}
