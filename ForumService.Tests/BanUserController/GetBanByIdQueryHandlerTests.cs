using ForumService.Contract.Models;
using ForumService.Core.Handler.BanUser.Query;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.BanUser.Query;
using ForumService.Domain.Models;

namespace ForumService.Tests.BanUserController
{
    public class GetBanByIdQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<ForumUserBan>> _banRepoMock;
        private readonly Mock<IKafkaProducerRepository<User>> _producerRepoMock;

        // System Under Test
        private readonly GetBanByIdQueryHandler _handler;

        public GetBanByIdQueryHandlerTests()
        {
            _banRepoMock = new Mock<IGenericRepository<ForumUserBan>>();
            _producerRepoMock = new Mock<IKafkaProducerRepository<User>>();

            _handler = new GetBanByIdQueryHandler(
                _banRepoMock.Object,
                _producerRepoMock.Object
            );
        }

        // Test Case 1: Ban Not Found
        // Scenario: Repository returns null.
        // Expected: Returns 404 Not Found.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_BanNotFound_ReturnsNotFound()
        {
            // Arrange
            var query = new GetBanByIdQuery(Guid.NewGuid());

            _banRepoMock.Setup(r => r.GetByIdAsync(query.BanId))
                .ReturnsAsync((ForumUserBan?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Không tìm thấy lệnh cấm.", result.Message);
            Assert.Null(result.ResponseData);
        }

        // Test Case 2: Happy Path - Success with User Enrichment
        // Scenario: Ban exists, and User Service returns user profiles.
        // Expected: Returns 200 OK with fully populated DTO (including names/avatars).
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_ReturnsBanDetailsWithUserInfo()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var bannedById = Guid.NewGuid();
            var unbannedById = Guid.NewGuid();

            var query = new GetBanByIdQuery(banId);

            var banEntity = new ForumUserBan
            {
                Id = banId,
                UserId = userId,
                Reason = "Spamming",
                BannedBy = bannedById,
                BannedAt = DateTime.UtcNow.AddDays(-5),
                BanUntil = DateTime.UtcNow.AddDays(5),
                IsActive = false,
                UnbannedAt = DateTime.UtcNow,
                UnbannedBy = unbannedById,
                UnbanReason = "Apology accepted"
            };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId))
                .ReturnsAsync(banEntity);

            // Mock User Service Response
            var users = new List<User>
            {
                new User { id = userId, firstName = "Bad", lastName = "User", avatarUrl = "user.png" },
                new User { id = bannedById, firstName = "Super", lastName = "Admin" },
                new User { id = unbannedById, firstName = "Kind", lastName = "Mod" }
            };

            // Setup generic Kafka call
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);

            var dto = result.ResponseData;

            // Check Ban Details
            Assert.Equal(banId, dto.Id);
            Assert.Equal("Spamming", dto.Reason);
            Assert.False(dto.IsActive);

            // Check User Enrichment
            Assert.Equal("Bad", dto.UserFirstName);
            Assert.Equal("user.png", dto.UserAvatarUrl);

            // Check Banner Enrichment
            Assert.Equal("Super", dto.BannedByFirstName);

            // Check Unbanner Enrichment (Logic handles UnbannedBy)
            // Note: The DTO in your code currently doesn't have fields like UnbannedByFirstName explicitly shown 
            // in the 'var banDto = new BanDto { ... }' block provided, but the handler fetches 'unbannerAdmin'.
            // If BanDto doesn't have those properties, they won't be mapped. 
            // Based on the provided code, only User and BannedBy fields are mapped.
            Assert.Equal(unbannedById, dto.UnbannedBy);
        }

        // Test Case 3: Resilience - User Service Failure
        // Scenario: Ban exists, but Kafka User Service throws exception.
        // Expected: Returns 200 OK with Ban Details, but User fields are null (Graceful Degradation).
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_UserServiceFails_ReturnsBanDetailsWithoutUserInfo()
        {
            // Arrange
            var banId = Guid.NewGuid();
            var query = new GetBanByIdQuery(banId);

            var banEntity = new ForumUserBan
            {
                Id = banId,
                UserId = Guid.NewGuid(),
                BannedBy = Guid.NewGuid(),
                IsActive = true
            };

            _banRepoMock.Setup(r => r.GetByIdAsync(banId))
                .ReturnsAsync(banEntity);

            // Mock Kafka Failure
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka Timeout"));

            // Act
            // Handler has a try-catch block specifically for user hydration
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);

            // Verify Ban ID is correct
            Assert.Equal(banId, result.ResponseData.Id);

            // Verify User fields are null due to failure
            Assert.Null(result.ResponseData.UserFirstName);
            Assert.Null(result.ResponseData.BannedByFirstName);
        }

        // Test Case 4: Exception Handling - Database Error
        // Scenario: Repository GetByIdAsync throws.
        // Expected: Returns 500 Internal Server Error.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DatabaseError_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetBanByIdQuery(Guid.NewGuid());

            _banRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new Exception("DB Connection Lost"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Equal("DB Connection Lost", result.Message);
            Assert.Null(result.ResponseData);
        }
    }
}
