using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Handler.Post.Command;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.ModeratorPostController
{
    public class RejectPostHandlerTests
    {
        // Mock Dependencies
        private readonly Mock<IGenericRepository<ForumService.Domain.Models.Post>> _postRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ISUtilityServiceClient> _utilityServiceMock;
        private readonly Mock<ILogger<RejectPostCommandHandler>> _loggerMock;

        // Handler under test
        private readonly RejectPostCommandHandler _handler;

        public RejectPostHandlerTests()
        {
            _postRepositoryMock = new Mock<IGenericRepository<ForumService.Domain.Models.Post>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _utilityServiceMock = new Mock<ISUtilityServiceClient>();
            _loggerMock = new Mock<ILogger<RejectPostCommandHandler>>();

            _handler = new RejectPostCommandHandler(
                _postRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _utilityServiceMock.Object,
                _loggerMock.Object
            );
        }

        // Test Case 1: Post Not Found
        [Fact]
        [Trait("Category", "RejectPostHandler - Validation")]
        public async Task Handle_WhenPostNotFound_Returns404()
        {
            // Arrange
            var command = new RejectPostCommand(Guid.NewGuid(), Guid.NewGuid(), "Violation of rules");
            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ForumService.Domain.Models.Post)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Post not found.", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 2: Post Deleted
        [Fact]
        [Trait("Category", "RejectPostHandler - Validation")]
        public async Task Handle_WhenPostIsDeleted_Returns404()
        {
            // Arrange
            var command = new RejectPostCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");
            var post = new ForumService.Domain.Models.Post { PostId = Guid.NewGuid(), IsDeleted = true };

            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 3: Invalid Status (Not PendingReview)
        [Fact]
        [Trait("Category", "RejectPostHandler - Validation")]
        public async Task Handle_WhenPostStatusIsNotPendingReview_Returns400()
        {
            // Arrange
            var command = new RejectPostCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");
            var post = new ForumService.Domain.Models.Post
            {
                PostId = Guid.NewGuid(),
                Status = "Published", // Đã public rồi thì không reject được nữa (theo logic hiện tại)
                IsDeleted = false
            };

            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("Only posts with 'PendingReview' status can be rejected", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 4: Happy Path (Reject + Update Reason + Notify)
        [Fact]
        [Trait("Category", "RejectPostHandler - Success")]
        public async Task Handle_WhenStatusIsPendingReview_ShouldRejectAndUpdateReasonAndNotify()
        {
            // Arrange
            var moderatorId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var rejectionReason = "Content violates policy guideline #3";

            var post = new ForumService.Domain.Models.Post
            {
                PostId = Guid.NewGuid(),
                AuthorId = authorId,
                Status = "PendingReview",
                Title = "Spam Post",
                IsDeleted = false
            };

            var command = new RejectPostCommand(post.PostId, moderatorId, rejectionReason);
            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Post rejected successfully.", result.Message);

            // 1. Check Post Update logic
            Assert.Equal("Rejected", post.Status);
            Assert.Equal(rejectionReason, post.RejectionReason); 
            Assert.Equal(moderatorId, post.ModeratedBy);
            Assert.Equal(moderatorId, post.UpdatedBy);

            _postRepositoryMock.Verify(x => x.UpdateAsync(post), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);

            // 2. Check Notification sent to Author
            // Verify notification contains the specific rejection reason
            _utilityServiceMock.Verify(x => x.SendNotificationAsync(
                It.Is<NotificationDto>(n =>
                    n.UserId == authorId &&
                    n.Title.Contains("rejected") &&
                    n.Message.Contains(rejectionReason) 
                ),
                It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // Test Case 5: Moderator is Author (No Notification)
        [Fact]
        [Trait("Category", "RejectPostHandler - Success")]
        public async Task Handle_WhenModeratorIsAuthor_ShouldRejectButNotNotify()
        {
            // Arrange
            var userId = Guid.NewGuid(); // Vừa là Author vừa là Moderator (tự reject bài mình?)
            var post = new ForumService.Domain.Models.Post
            {
                PostId = Guid.NewGuid(),
                AuthorId = userId,
                Status = "PendingReview",
                IsDeleted = false
            };

            var command = new RejectPostCommand(post.PostId, userId, "Self rejection");
            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Rejected", post.Status);

            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);

            // Verify Notification is NEVER sent
            _utilityServiceMock.Verify(x => x.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 6: Notification Failure (Failover)
        [Fact]
        [Trait("Category", "RejectPostHandler - NotificationFail")]
        public async Task Handle_WhenNotificationFails_ShouldStillReturnSuccessAndLog()
        {
            // Arrange
            var moderatorId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var post = new ForumService.Domain.Models.Post
            {
                PostId = Guid.NewGuid(),
                AuthorId = authorId,
                Status = "PendingReview",
                Title = "Test Post"
            };

            var command = new RejectPostCommand(post.PostId, moderatorId, "Reason");
            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);

            // Mock Notification Exception
            _utilityServiceMock.Setup(x => x.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Notification Service Timeout"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status); 
            Assert.Equal("Rejected", post.Status);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);

            // Verify Error Logger
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        // Test Case 7: Database Error
        [Fact]
        [Trait("Category", "RejectPostHandler - Exception")]
        public async Task Handle_WhenDbCommitFails_Returns500()
        {
            // Arrange
            var post = new ForumService.Domain.Models.Post
            {
                PostId = Guid.NewGuid(),
                Status = "PendingReview"
            };
            var command = new RejectPostCommand(post.PostId, Guid.NewGuid(), "Reason");

            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(post);

            // Mock DB Error
            _unitOfWorkMock.Setup(x => x.CommitAsync()).ThrowsAsync(new Exception("DB Connection Error"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("DB Connection Error", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }
    }
}
