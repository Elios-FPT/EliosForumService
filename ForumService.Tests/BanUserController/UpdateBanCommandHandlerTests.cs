using ForumService.Core.Handler.BanUser.Command;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.BanUser.Command;
using ForumService.Domain.Models;

namespace ForumService.Tests.BanUserController
{
    public class UpdateBanCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<ForumUserBan>> _banRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        // System Under Test
        private readonly UpdateBanCommandHandler _handler;

        public UpdateBanCommandHandlerTests()
        {
            _banRepoMock = new Mock<IGenericRepository<ForumUserBan>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new UpdateBanCommandHandler(
                _banRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        // Test Case 1: Ban Not Found
        // Scenario: Repository returns null.
        // Expected: Returns 404.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_BanNotFound_ReturnsNotFound()
        {
            // Arrange
            var command = new UpdateBanCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason", null);

            _banRepoMock.Setup(r => r.GetByIdAsync(command.BanId))
                .ReturnsAsync((ForumUserBan?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Không tìm thấy lệnh cấm.", result.Message);
            Assert.False(result.ResponseData);
        }

        // Test Case 2: Invalid Date
        // Scenario: BanUntil is in the past.
        // Expected: Returns 400.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_BanUntilInPast_ReturnsBadRequest()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var command = new UpdateBanCommand(banId, Guid.NewGuid(), "Reason", DateTime.UtcNow.AddMinutes(-1));

            var existingBan = new ForumUserBan { Id = banId };
            _banRepoMock.Setup(r => r.GetByIdAsync(banId)).ReturnsAsync(existingBan);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Thời hạn cấm mới phải lớn hơn thời điểm hiện tại.", result.Message);
        }

        // Test Case 3: Happy Path - Standard Update
        // Scenario: Updating details of an already active ban.
        // Expected: Updates fields, commits transaction.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidUpdate_UpdatesBanDetails()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var newDate = DateTime.UtcNow.AddDays(5);
            var command = new UpdateBanCommand(banId, Guid.NewGuid(), "New Reason", newDate);

            var existingBan = new ForumUserBan
            {
                Id = banId,
                IsActive = true,
                Reason = "Old Reason",
                BanUntil = DateTime.UtcNow.AddDays(1)
            };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId)).ReturnsAsync(existingBan);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);

            // Verify changes
            Assert.Equal("New Reason", existingBan.Reason);
            Assert.Equal(newDate, existingBan.BanUntil);
            Assert.True(existingBan.IsActive); // Remains active

            // Verify Transaction
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _banRepoMock.Verify(r => r.UpdateAsync(existingBan), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        // Test Case 4: Happy Path - Reactivation Logic
        // Scenario: Updating an INACTIVE ban with a future date (or permanent).
        // Expected: Sets IsActive = true, clears Unbanned fields.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_UpdateInactiveBanWithFutureDate_ReactivatesBan()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var command = new UpdateBanCommand(banId, Guid.NewGuid(), "Reactivating", null); // Null = Permanent

            var existingBan = new ForumUserBan
            {
                Id = banId,
                IsActive = false, // Currently inactive
                UnbannedAt = DateTime.UtcNow.AddDays(-1),
                UnbannedBy = Guid.NewGuid(),
                UnbanReason = "Forgiven"
            };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId)).ReturnsAsync(existingBan);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);

            // Verify Reactivation Logic
            Assert.True(existingBan.IsActive); // Should be flipped to True
            Assert.Null(existingBan.UnbannedAt); // Cleared
            Assert.Null(existingBan.UnbannedBy); // Cleared
            Assert.Null(existingBan.UnbanReason); // Cleared
            Assert.Null(existingBan.BanUntil); // Updated to permanent as per command

            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        // Test Case 5: Exception Handling
        // Scenario: DB Update fails.
        // Expected: Returns 500, Rollback.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DatabaseError_RollsBackTransaction()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var command = new UpdateBanCommand(banId, Guid.NewGuid(), "Reason", null);
            var existingBan = new ForumUserBan { Id = banId, IsActive = true };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId)).ReturnsAsync(existingBan);

            // Mock DB Error
            _banRepoMock.Setup(r => r.UpdateAsync(It.IsAny<ForumUserBan>()))
                .ThrowsAsync(new Exception("DB Connection Lost"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Lỗi cập nhật", result.Message);

            // Verify Rollback
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}
