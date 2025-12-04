using ForumService.Core.Handler.BanUser.Command;
using ForumService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.BanUser.Command;
using ForumService.Contract.TransferObjects;

namespace ForumService.Tests.BanUserController
{
    public class CreateBanUserCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.ForumUserBan>> _banRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ISUtilityServiceClient> _utilityServiceMock;
        private readonly Mock<ILogger<CreateBanUserCommandHandler>> _loggerMock;

        // System Under Test
        private readonly CreateBanUserCommandHandler _handler;

        public CreateBanUserCommandHandlerTests()
        {
            _banRepoMock = new Mock<IGenericRepository<Domain.Models.ForumUserBan>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _utilityServiceMock = new Mock<ISUtilityServiceClient>();
            _loggerMock = new Mock<ILogger<CreateBanUserCommandHandler>>();

            _handler = new CreateBanUserCommandHandler(
                _banRepoMock.Object,
                _unitOfWorkMock.Object,
                _utilityServiceMock.Object,
                _loggerMock.Object
            );
        }

        // Test Case 1: Validation - Self Ban
        // Scenario: UserId == BannedBy
        // Expected: Returns 400 Bad Request.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_UserBansSelf_ReturnsBadRequest()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new CreateBanUserCommand(userId, "Spam", userId, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Bạn không thể tự cấm chính mình.", result.Message);
            Assert.Equal(Guid.Empty, result.ResponseData);

            // Verify Repo not called
            _banRepoMock.Verify(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.ForumUserBan, bool>>>(), null, null), Times.Never);
        }

        // Test Case 2: Validation - Invalid Date
        // Scenario: BanUntil is in the past.
        // Expected: Returns 400 Bad Request.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_BanDateInPast_ReturnsBadRequest()
        {
            // Arrange
            var command = new CreateBanUserCommand(Guid.NewGuid(), "Spam", Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Thời gian hết hạn cấm phải ở tương lai.", result.Message);
        }

        // Test Case 3: Business Rule - User Already Banned
        // Scenario: Repository finds an active ban for this user.
        // Expected: Returns 409 Conflict.
        [Fact]
        [Trait("Category", "Handler - BusinessRule")]
        public async Task Handle_UserAlreadyBanned_ReturnsConflict()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new CreateBanUserCommand(userId, "Spam", Guid.NewGuid(), null);
            var existingBan = new Domain.Models.ForumUserBan { Id = Guid.NewGuid(), UserId = userId, IsActive = true };

            // Mock finding existing ban
            _banRepoMock.Setup(x => x.GetOneAsync(
                It.IsAny<Expression<Func<Domain.Models.ForumUserBan, bool>>>(),
                null, null
            )).ReturnsAsync(existingBan);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(409, result.Status);
            Assert.Contains("Người dùng này đang bị cấm", result.Message);
            Assert.Equal(existingBan.Id, result.ResponseData); // Should return existing ID

            // Verify no new ban added
            _banRepoMock.Verify(x => x.AddAsync(It.IsAny<Domain.Models.ForumUserBan>()), Times.Never);
        }

        // Test Case 4: Happy Path - Permanent Ban
        // Scenario: Valid request, no BanUntil date.
        // Expected: Returns 201 Created, BanUntil is null, Notification sent.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidPermanentBan_CreatesBanAndNotifies()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var command = new CreateBanUserCommand(userId, "Violating terms", adminId, null);

            // Mock no existing ban
            _banRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.ForumUserBan, bool>>>(), null, null))
                .ReturnsAsync((Domain.Models.ForumUserBan?)null);

            // Capture the ban entity to verify properties
            Domain.Models.ForumUserBan capturedBan = null;
            _banRepoMock.Setup(x => x.AddAsync(It.IsAny<Domain.Models.ForumUserBan>()))
                .Callback<Domain.Models.ForumUserBan>(b => capturedBan = b)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert Response
            Assert.Equal(201, result.Status);
            Assert.Contains("vĩnh viễn", result.Message);
            Assert.NotEqual(Guid.Empty, result.ResponseData);

            // Verify Ban Entity
            Assert.NotNull(capturedBan);
            Assert.Equal(userId, capturedBan.UserId);
            Assert.Equal(adminId, capturedBan.BannedBy);
            Assert.Null(capturedBan.BanUntil); // Permanent
            Assert.True(capturedBan.IsActive);

            // Verify Transaction Flow
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);

            // Verify Notification Sent
            _utilityServiceMock.Verify(u => u.SendNotificationAsync(
                It.Is<NotificationDto>(n => n.UserId == userId && n.Title.Contains("khóa tài khoản")),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        // Test Case 5: Happy Path - Temporary Ban
        // Scenario: Valid request with future BanUntil date.
        // Expected: Returns 201 Created, BanUntil is set correctly.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidTemporaryBan_CreatesBanWithExpiry()
        {
            // Arrange
            var banUntil = DateTime.UtcNow.AddDays(7);
            var command = new CreateBanUserCommand(Guid.NewGuid(), "Warning", Guid.NewGuid(), banUntil);

            _banRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.ForumUserBan, bool>>>(), null, null))
                .ReturnsAsync((Domain.Models.ForumUserBan?)null);

            Domain.Models.ForumUserBan capturedBan = null;
            _banRepoMock.Setup(x => x.AddAsync(It.IsAny<Domain.Models.ForumUserBan>()))
                .Callback<Domain.Models.ForumUserBan>(b => capturedBan = b);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(201, result.Status);
            Assert.Contains($"đến {banUntil}", result.Message); // Check message format

            Assert.NotNull(capturedBan);
            Assert.Equal(banUntil, capturedBan.BanUntil);
        }

        // Test Case 6: Resilience - Notification Failure
        // Scenario: Ban is saved, but UtilityService throws exception.
        // Expected: Returns 201 (Success) anyway, but logs error.
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_NotificationFails_StillReturnsSuccessAndLogsError()
        {
            // Arrange
            var command = new CreateBanUserCommand(Guid.NewGuid(), "Reason", Guid.NewGuid(), null);

            _banRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.ForumUserBan, bool>>>(), null, null))
                .ReturnsAsync((Domain.Models.ForumUserBan?)null);

            // Mock Notification failure
            _utilityServiceMock.Setup(u => u.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Utility Service Down"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(201, result.Status); // Logic dictates ban is successful even if notify fails

            // Verify Transaction Committed (Ban was saved)
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);

            // Verify Logger called
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("gửi thông báo thất bại")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // Test Case 7: Exception Handling - DB Error
        // Scenario: Repository AddAsync fails.
        // Expected: Returns 500, Transaction Rolled Back.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var command = new CreateBanUserCommand(Guid.NewGuid(), "Reason", Guid.NewGuid(), null);

            _banRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.ForumUserBan, bool>>>(), null, null))
                .ReturnsAsync((Domain.Models.ForumUserBan?)null);

            // Mock DB Error
            _banRepoMock.Setup(x => x.AddAsync(It.IsAny<Domain.Models.ForumUserBan>()))
                .ThrowsAsync(new Exception("DB Connection Timeout"));

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
