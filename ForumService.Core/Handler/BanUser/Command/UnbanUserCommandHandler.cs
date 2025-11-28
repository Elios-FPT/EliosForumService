using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ForumService.Core.Handler.BanUser.Command
{
    public class UnbanUserCommandHandler : ICommandHandler<Contract.UseCases.BanUser.Command.UnbanUserCommand, BaseResponseDto<bool>>
    {
        private readonly IGenericRepository<ForumUserBan> _banRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient; 
        private readonly ILogger<UnbanUserCommandHandler> _logger; 

        public UnbanUserCommandHandler(
            IGenericRepository<ForumUserBan> banRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient,
            ILogger<UnbanUserCommandHandler> logger)
        {
            _banRepository = banRepository ?? throw new ArgumentNullException(nameof(banRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BaseResponseDto<bool>> Handle(Contract.UseCases.BanUser.Command.UnbanUserCommand request, CancellationToken cancellationToken)
        {
            var ban = await _banRepository.GetByIdAsync(request.BanId);
            if (ban == null)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 404,
                    Message = "Không tìm thấy lệnh cấm này.",
                    ResponseData = false
                };
            }

            if (!ban.IsActive)
            {
                return new BaseResponseDto<bool>
                {
                    Status = 400,
                    Message = "Người dùng này đã được gỡ cấm hoặc lệnh cấm đã hết hạn trước đó.",
                    ResponseData = false
                };
            }

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                ban.IsActive = false;
                ban.UnbannedAt = DateTime.UtcNow;
                ban.UnbannedBy = request.UnbannedBy;
                ban.UnbanReason = request.UnbanReason;

                await _banRepository.UpdateAsync(ban);
                await _unitOfWork.CommitAsync();

                // 4. (Notification Logic)
                try
                {
                    if (ban.UserId != request.UnbannedBy)
                    {
                        string title = "Thông báo mở khóa tài khoản";
                        string message = $"Tài khoản diễn đàn của bạn đã được mở khóa. Lý do mở khóa: {request.UnbanReason ?? "Được xem xét bởi quản trị viên"}";

                        var metadataDict = new Dictionary<string, string>
                        {
                            { "BanId", ban.Id.ToString() },
                            { "UnbannedBy", request.UnbannedBy.ToString() },
                            { "Type", "UnbanNotification" }
                        };

                        var notificationRequest = new NotificationDto
                        {
                            UserId = ban.UserId, 
                            Title = title,
                            Message = message,
                            Url = "/", 
                            Metadata = JsonSerializer.Serialize(metadataDict)
                        };

                        await _utilityServiceClient.SendNotificationAsync(notificationRequest, cancellationToken);
                    }
                }
                catch (Exception notifyEx)
                {
                    _logger.LogError(notifyEx, "Đã gỡ cấm thành công cho BanId {BanId} nhưng thất bại khi gửi thông báo.", ban.Id);
                }

                return new BaseResponseDto<bool>
                {
                    Status = 200,
                    Message = "Đã gỡ lệnh cấm thành công.",
                    ResponseData = true
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<bool>
                {
                    Status = 500,
                    Message = $"Lỗi hệ thống: {ex.Message}",
                    ResponseData = false
                };
            }
        }
    }
}
