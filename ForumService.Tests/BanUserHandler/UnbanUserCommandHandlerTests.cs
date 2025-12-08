using ForumService.Core.Handler.BanUser.Command;
using ForumService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.BanUser.Command;
using ForumService.Domain.Models;
using ForumService.Contract.TransferObjects;

namespace ForumService.Tests.BanUserHandler
{
    public class UnbanUserCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<ForumUserBan>> _banRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ISUtilityServiceClient> _utilityServiceMock;
        private readonly Mock<ILogger<UnbanUserCommandHandler>> _loggerMock;

        // System Under Test
        private readonly UnbanUserCommandHandler _handler;

        public UnbanUserCommandHandlerTests()
        {
            _banRepoMock = new Mock<IGenericRepository<ForumUserBan>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _utilityServiceMock = new Mock<ISUtilityServiceClient>();
            _loggerMock = new Mock<ILogger<UnbanUserCommandHandler>>();

            _handler = new UnbanUserCommandHandler(
                _banRepoMock.Object,
                _unitOfWorkMock.Object,
                _utilityServiceMock.Object,
                _loggerMock.Object
            );
        }

        // Test Case 1: Ban Not Found
        // Scenario: Repository returns null for the given BanId.
        // Expected: Returns 404 Not Found.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_BanNotFound_ReturnsNotFound()
        {
            // Arrange
            var command = new UnbanUserCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");

            _banRepoMock.Setup(r => r.GetByIdAsync(command.BanId))
                .ReturnsAsync((ForumUserBan?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Không tìm thấy lệnh cấm này.", result.Message);
            Assert.False(result.ResponseData);
        }

        // Test Case 2: Ban Already Inactive
        // Scenario: Ban exists but IsActive is false.
        // Expected: Returns 400 Bad Request.
        [Fact]
        [Trait("Category", "Handler - BusinessRule")]
        public async Task Handle_BanAlreadyInactive_ReturnsBadRequest()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var command = new UnbanUserCommand(banId, Guid.NewGuid(), "Reason");
            var existingBan = new ForumUserBan { Id = banId, IsActive = false };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId))
                .ReturnsAsync(existingBan);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("lệnh cấm đã hết hạn trước đó", result.Message);
            Assert.False(result.ResponseData);

            // Verify no updates attempted
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 3: Happy Path - Success
        // Scenario: Active ban exists.
        // Expected: Updates ban status, sets unban details, commits transaction, sends notification.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_UnbansSuccessfullyAndNotifies()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var adminId = Guid.NewGuid(); // Different from UserId
            var reason = "Appealed successfully";

            var command = new UnbanUserCommand(banId, adminId, reason);

            var existingBan = new ForumUserBan
            {
                Id = banId,
                UserId = userId,
                IsActive = true
            };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId)).ReturnsAsync(existingBan);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert Response
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Đã gỡ lệnh cấm thành công.", result.Message);

            // Verify State Changes
            Assert.False(existingBan.IsActive);
            Assert.Equal(adminId, existingBan.UnbannedBy);
            Assert.Equal(reason, existingBan.UnbanReason);
            Assert.NotNull(existingBan.UnbannedAt);
            Assert.True((DateTime.UtcNow - existingBan.UnbannedAt.Value).TotalSeconds < 1);

            // Verify Transaction Flow
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _banRepoMock.Verify(r => r.UpdateAsync(existingBan), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);

            // Verify Notification
            _utilityServiceMock.Verify(u => u.SendNotificationAsync(
                It.Is<NotificationDto>(n => n.UserId == userId && n.Title.Contains("mở khóa")),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        // Test Case 4: Resilience - Notification Failure
        // Scenario: Unban logic succeeds, but notification service throws error.
        // Expected: Returns 200 (Success), logs error.
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_NotificationFails_StillReturnsSuccessAndLogsError()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var command = new UnbanUserCommand(banId, Guid.NewGuid(), "Reason");
            var existingBan = new ForumUserBan { Id = banId, UserId = Guid.NewGuid(), IsActive = true };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId)).ReturnsAsync(existingBan);

            // Mock Notification Failure
            _utilityServiceMock.Setup(u => u.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Utility Service Timeout"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status); // Should still succeed

            // Verify Transaction Committed
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);

            // Verify Error Logging
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("thất bại khi gửi thông báo")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // Test Case 5: Exception Handling - DB Error
        // Scenario: UpdateAsync throws exception.
        // Expected: Returns 500, Transaction Rolled Back.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var command = new UnbanUserCommand(banId, Guid.NewGuid(), "Reason");
            var existingBan = new ForumUserBan { Id = banId, IsActive = true };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId)).ReturnsAsync(existingBan);

            // Mock DB Error
            _banRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ForumUserBan>()))
                .ThrowsAsync(new Exception("DB Deadlock"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Lỗi hệ thống", result.Message);

            // Verify Rollback
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}
