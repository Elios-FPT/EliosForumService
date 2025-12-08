using ForumService.Core.Handler.BanUser.Query;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Tests.BanUserHandler
{
    public class GetMyBanStatusQueryHandlerTests
    {
        private readonly Mock<IGenericRepository<ForumUserBan>> _mockBanRepository;
        private readonly GetMyBanStatusQueryHandler _handler;

        public GetMyBanStatusQueryHandlerTests()
        {
            _mockBanRepository = new Mock<IGenericRepository<ForumUserBan>>();
            _handler = new GetMyBanStatusQueryHandler(_mockBanRepository.Object);
        }

        private void SetupGetListAsyncReturns(IEnumerable<ForumUserBan> returns)
        {
            _mockBanRepository.Setup(repo => repo.GetListAsync(
                It.IsAny<Expression<Func<ForumUserBan, bool>>>(),
                It.IsAny<Expression<Func<IQueryable<ForumUserBan>, IOrderedQueryable<ForumUserBan>>>>(),
                It.IsAny<Expression<Func<IQueryable<ForumUserBan>, IQueryable<ForumUserBan>>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()
            )).ReturnsAsync(returns);
        }

        [Fact]
        public async Task Handle_UserBannedWithDuration_ReturnsIsBannedTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new Contract.UseCases.BanUser.Query.GetMyBanStatusQuery(userId);
            var futureDate = DateTime.UtcNow.AddDays(5);    

            var bans = new List<ForumUserBan>
            {
                new ForumUserBan
                {
                    UserId = userId,
                    IsActive = true,
                    Reason = "Spamming",
                    BanUntil = futureDate
                }
            };

            SetupGetListAsyncReturns(bans);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.True(result.ResponseData.IsBanned);
            Assert.Equal("Spamming", result.ResponseData.Reason);
            Assert.Equal(futureDate, result.ResponseData.BanUntil);
            Assert.False(result.ResponseData.IsPermanent);
        }

        [Fact]
        public async Task Handle_UserBannedPermanently_ReturnsIsPermanentTrue()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new Contract.UseCases.BanUser.Query.GetMyBanStatusQuery(userId);

            var bans = new List<ForumUserBan>
            {
                new ForumUserBan
                {
                    UserId = userId,
                    IsActive = true,
                    Reason = "Severe Violation",
                    BanUntil = null 
                }
            };

            SetupGetListAsyncReturns(bans);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData.IsBanned);
            Assert.Null(result.ResponseData.BanUntil);
            Assert.True(result.ResponseData.IsPermanent);
        }

        [Fact]
        public async Task Handle_UserNotBanned_NoRecords_ReturnsIsBannedFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new Contract.UseCases.BanUser.Query.GetMyBanStatusQuery(userId);

            SetupGetListAsyncReturns(new List<ForumUserBan>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.False(result.ResponseData.IsBanned);
            Assert.Null(result.ResponseData.Reason);
        }

        [Fact]
        public async Task Handle_UserNotBanned_BanExpired_ReturnsIsBannedFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new Contract.UseCases.BanUser.Query.GetMyBanStatusQuery(userId);

            var bans = new List<ForumUserBan>
            {
                new ForumUserBan
                {
                    UserId = userId,
                    IsActive = true,
                    BanUntil = DateTime.UtcNow.AddDays(-1)
                }
            };

            SetupGetListAsyncReturns(bans);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.False(result.ResponseData.IsBanned);
        }

        [Fact]
        public async Task Handle_UserNotBanned_BanIsInactive_ReturnsIsBannedFalse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new Contract.UseCases.BanUser.Query.GetMyBanStatusQuery(userId);

            var bans = new List<ForumUserBan>
            {
                new ForumUserBan
                {
                    UserId = userId,
                    IsActive = false,
                    BanUntil = DateTime.UtcNow.AddDays(10)
                }
            };

            SetupGetListAsyncReturns(bans);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.False(result.ResponseData.IsBanned);
        }

        [Fact]
        public async Task Handle_RepositoryThrowsException_ReturnsStatus500()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new Contract.UseCases.BanUser.Query.GetMyBanStatusQuery(userId);

            _mockBanRepository.Setup(repo => repo.GetListAsync(
                It.IsAny<Expression<Func<ForumUserBan, bool>>>(),
                It.IsAny<Expression<Func<IQueryable<ForumUserBan>, IOrderedQueryable<ForumUserBan>>>>(),
                It.IsAny<Expression<Func<IQueryable<ForumUserBan>, IQueryable<ForumUserBan>>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()
            )).ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Database connection failed", result.Message);
            Assert.Null(result.ResponseData);
        }
    }
}
