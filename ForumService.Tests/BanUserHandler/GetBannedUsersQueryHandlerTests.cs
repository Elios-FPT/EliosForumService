using ForumService.Contract.Models;
using ForumService.Core.Handler.BanUser.Query;
using ForumService.Core.Interfaces;
using Moq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.BanUser.Query;
using ForumService.Domain.Models;

namespace ForumService.Tests.BanUserHandler
{
    public class GetBannedUsersQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<ForumUserBan>> _banRepoMock;
        private readonly Mock<IKafkaProducerRepository<User>> _producerRepoMock;

        // System Under Test
        private readonly GetBannedUsersQueryHandler _handler;

        public GetBannedUsersQueryHandlerTests()
        {
            _banRepoMock = new Mock<IGenericRepository<ForumUserBan>>();
            _producerRepoMock = new Mock<IKafkaProducerRepository<User>>();

            _handler = new GetBannedUsersQueryHandler(
                _banRepoMock.Object,
                _producerRepoMock.Object
            );
        }

        // Test Case 1: Happy Path - List returned with User Enrichment
        // Scenario: Bans exist, User Service returns profiles.
        // Expected: Returns 200 OK, mapped DTOs with names/avatars.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_ReturnsPagedListWithUserInfo()
        {
            // Arrange
            var query = new GetBannedUsersQuery(null, null, 1, 10);
            var userId1 = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var bans = new List<ForumUserBan>
            {
                new ForumUserBan { Id = Guid.NewGuid(), UserId = userId1, BannedBy = adminId, BannedAt = DateTime.UtcNow }
            };

            _banRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>()))
                .ReturnsAsync(1);

            _banRepoMock.Setup(r => r.GetListAsync(
                    It.IsAny<Expression<Func<ForumUserBan, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<ForumUserBan>, IOrderedQueryable<ForumUserBan>>>>(), // OrderBy
                    null, // Include
                    10, // PageSize
                    1   // PageNumber
                ))
                .ReturnsAsync(bans);

            // Mock User Service
            var users = new List<User>
            {
                new User { id = userId1, firstName = "User", lastName = "One" },
                new User { id = adminId, firstName = "Admin", lastName = "User" }
            };
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Single(result.ResponseData);

            var dto = result.ResponseData.First();
            Assert.Equal("User", dto.UserFirstName);
            Assert.Equal("Admin", dto.BannedByFirstName);
            Assert.Equal(1, result.Pagination.TotalItems);
        }

        // Test Case 2: Empty State
        // Scenario: No bans found matching filter.
        // Expected: Returns 200 OK, Empty List, "No banned users found".
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_NoBansFound_ReturnsEmptyList()
        {
            // Arrange
            var query = new GetBannedUsersQuery(null, null, 1, 10);

            _banRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No banned users found.", result.Message);

            // Verify List query skipped
            _banRepoMock.Verify(r => r.GetListAsync(
                It.IsAny<Expression<Func<ForumUserBan, bool>>>(), null, null, It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
        }

        // Test Case 3: Filter Logic - UserId Only
        // Scenario: Query has UserId but IsActive is null.
        // Expected: Repo called with filter checking UserId.
        [Fact]
        [Trait("Category", "Handler - Filter")]
        public async Task Handle_FilterByUserId_AppliesCorrectFilter()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new GetBannedUsersQuery(userId, null, 1, 10);

            _banRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>()))
                .ReturnsAsync(1);
            _banRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>(), null, null, 10, 1))
                .ReturnsAsync(new List<ForumUserBan> { new ForumUserBan() });
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            // Act
            await _handler.Handle(query, CancellationToken.None);

            // Assert
            _banRepoMock.Verify(r => r.GetCountAsync(It.IsNotNull<Expression<Func<ForumUserBan, bool>>>()), Times.Once);
        }

        // Test Case 4: Filter Logic - IsActive Only
        // Scenario: Query has IsActive = true.
        // Expected: Repo called with filter checking IsActive.
        [Fact]
        [Trait("Category", "Handler - Filter")]
        public async Task Handle_FilterByIsActive_AppliesCorrectFilter()
        {
            // Arrange
            var query = new GetBannedUsersQuery(null, true, 1, 10);

            _banRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>()))
                .ReturnsAsync(1);
            _banRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>(), null, null, 10, 1))
                .ReturnsAsync(new List<ForumUserBan> { new ForumUserBan() });
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            // Act
            await _handler.Handle(query, CancellationToken.None);

            // Assert
            _banRepoMock.Verify(r => r.GetCountAsync(It.IsNotNull<Expression<Func<ForumUserBan, bool>>>()), Times.Once);
        }

        // Test Case 5: Resilience - User Service Failure
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_UserServiceFails_ReturnsBansWithoutUserInfo()
        {
            // Arrange
            var query = new GetBannedUsersQuery(null, null, 1, 10);
            var banId = Guid.NewGuid();

            _banRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>()))
                .ReturnsAsync(1);

            _banRepoMock.Setup(r => r.GetListAsync(
                    It.IsAny<Expression<Func<ForumUserBan, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<ForumUserBan>, IOrderedQueryable<ForumUserBan>>>>(), 
                    null,
                    10,
                    1))
                .ReturnsAsync(new List<ForumUserBan> { new ForumUserBan { Id = banId } });

            // Fail Kafka
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka Timeout"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Single(result.ResponseData); 

            var dto = result.ResponseData.First();
            Assert.Equal(banId, dto.Id);
            Assert.Null(dto.UserFirstName); 
        }

        // Test Case 6: Exception Handling - DB Error
        // Scenario: Repo GetCountAsync throws.
        // Expected: Returns 500.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DatabaseError_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetBannedUsersQuery(null, null, 1, 10);

            _banRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<ForumUserBan, bool>>>()))
                .ThrowsAsync(new Exception("DB Connection Lost"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Query error", result.Message);
            Assert.Empty(result.ResponseData);
        }
    }
}
