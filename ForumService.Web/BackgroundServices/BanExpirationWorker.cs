using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ForumService.Web.BackgroundServices
{
    public class BanExpirationWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BanExpirationWorker> _logger;

        // Cấu hình thời gian quét: Ví dụ 1 phút chạy 1 lần
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public BanExpirationWorker(IServiceProvider serviceProvider, ILogger<BanExpirationWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Ban Expiration Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessExpiredBansAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing expired bans.");
                }

                // Chờ đến lần quét tiếp theo
                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("Ban Expiration Worker is stopping.");
        }

        private async Task ProcessExpiredBansAsync()
        {
            // Vì BackgroundService là Singleton, còn Repository là Scoped, 
            // ta phải tạo scope mới mỗi khi chạy.
            using (var scope = _serviceProvider.CreateScope())
            {
                var banRepository = scope.ServiceProvider.GetRequiredService<IGenericRepository<ForumUserBan>>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // 1. Tìm các lệnh cấm đang Active nhưng thời hạn đã qua
                // Điều kiện: IsActive = true AND BanUntil <= Now
                // Sử dụng GetListAsync thay vì GetListAsyncUntracked để khớp với Interface IGenericRepository
                var expiredBans = await banRepository.GetListAsync(
                    filter: b => b.IsActive && b.BanUntil.HasValue && b.BanUntil.Value <= DateTime.UtcNow
                );

                if (expiredBans != null && expiredBans.Any())
                {
                    _logger.LogInformation($"Found {expiredBans.Count()} expired bans. Updating...");

                    await unitOfWork.BeginTransactionAsync();

                    try
                    {
                        foreach (var ban in expiredBans)
                        {
                            // Cập nhật trạng thái
                            ban.IsActive = false;

                            // Ghi nhận hệ thống tự động gỡ
                            ban.UnbannedAt = DateTime.UtcNow;
                            ban.UnbanReason = "System Auto-Expiration (Expired)";
                            // UnbannedBy có thể để null hoặc gán một GUID cố định của System

                            await banRepository.UpdateAsync(ban);
                        }

                        await unitOfWork.CommitAsync();
                        _logger.LogInformation("Successfully updated expired bans.");
                    }
                    catch (Exception)
                    {
                        await unitOfWork.RollbackAsync();
                        throw; // Ném ra để log ở vòng ngoài
                    }
                }
            }
        }
    }
}
