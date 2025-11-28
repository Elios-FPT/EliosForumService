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
using static ForumService.Contract.UseCases.BanUser.Command;

namespace ForumService.Core.Handler.BanUser.Command
{
    public class CreateBanUserCommandHandler : ICommandHandler<CreateBanUserCommand, BaseResponseDto<Guid>>
    {
        private readonly IGenericRepository<ForumUserBan> _banRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISUtilityServiceClient _utilityServiceClient; 
        private readonly ILogger<CreateBanUserCommandHandler> _logger;

        public CreateBanUserCommandHandler(
            IGenericRepository<ForumUserBan> banRepository,
            IUnitOfWork unitOfWork,
            ISUtilityServiceClient utilityServiceClient,
            ILogger<CreateBanUserCommandHandler> logger)
        {
            _banRepository = banRepository ?? throw new ArgumentNullException(nameof(banRepository));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _utilityServiceClient = utilityServiceClient ?? throw new ArgumentNullException(nameof(utilityServiceClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BaseResponseDto<Guid>> Handle(CreateBanUserCommand request, CancellationToken cancellationToken)
        {
            // --- 1. Validation ---
            if (request.BannedBy == request.UserId)
            {
                return new BaseResponseDto<Guid> { Status = 400, Message = "Bạn không thể tự cấm chính mình.", ResponseData = Guid.Empty };
            }

            if (request.BanUntil.HasValue && request.BanUntil.Value <= DateTime.UtcNow)
            {
                return new BaseResponseDto<Guid> { Status = 400, Message = "Thời gian hết hạn cấm phải ở tương lai.", ResponseData = Guid.Empty };
            }

            try
            {
                var existingBan = await _banRepository.GetOneAsync(
                    filter: b => b.UserId == request.UserId && b.IsActive == true
                );

                if (existingBan != null)
                {
                    return new BaseResponseDto<Guid> { Status = 409, Message = "Người dùng này đang bị cấm. Vui lòng cập nhật lệnh cấm cũ.", ResponseData = existingBan.Id };
                }

                await _unitOfWork.BeginTransactionAsync();

                // --- 2. Action ---
                var newBan = new ForumUserBan
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    Reason = request.Reason,
                    BannedBy = request.BannedBy,
                    BannedAt = DateTime.UtcNow,
                    BanUntil = request.BanUntil,
                    IsActive = true
                };

                await _banRepository.AddAsync(newBan);
                await _unitOfWork.CommitAsync();

                // --- 3. Notification Logic ---
                try
                {
                    string durationText = request.BanUntil.HasValue
                        ? $"đến {request.BanUntil.Value:dd/MM/yyyy HH:mm}"
                        : "vĩnh viễn";

                    string title = "Thông báo khóa tài khoản diễn đàn";
                    string message = $"Tài khoản của bạn đã bị khóa quyền truy cập diễn đàn {durationText}. Lý do: {request.Reason}";

                    var metadataDict = new Dictionary<string, string>
                    {
                        { "BanId", newBan.Id.ToString() },
                        { "TriggeredByUserId", request.BannedBy.ToString() },
                        { "Reason", request.Reason },
                        { "BanUntil", request.BanUntil?.ToString() ?? "Permanent" }
                    };

                    var notificationRequest = new NotificationDto
                    {
                        UserId = request.UserId, 
                        Title = title,
                        Message = message,
                        Url = "/policy", 
                        Metadata = JsonSerializer.Serialize(metadataDict)
                    };

                    await _utilityServiceClient.SendNotificationAsync(notificationRequest, cancellationToken);
                }
                catch (Exception notifyEx)
                {
                    _logger.LogError(notifyEx, "Đã ban User {UserId} thành công nhưng gửi thông báo thất bại.", request.UserId);
                }

                return new BaseResponseDto<Guid>
                {
                    Status = 201,
                    Message = request.BanUntil.HasValue
                        ? $"Đã cấm người dùng đến {request.BanUntil.Value}."
                        : "Đã cấm người dùng vĩnh viễn.",
                    ResponseData = newBan.Id
                };
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackAsync();
                return new BaseResponseDto<Guid> { Status = 500, Message = $"Lỗi hệ thống: {ex.Message}", ResponseData = Guid.Empty };
            }
        }
    }
}
