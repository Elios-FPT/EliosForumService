using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Handler.Post.Command;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostHandler
{
    public class UpvotePostHandlerTests
    {
        private readonly Mock<IGenericRepository<ForumService.Domain.Models.Post>> _postRepositoryMock;
        private readonly Mock<IGenericRepository<Vote>> _voteRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ISUtilityServiceClient> _utilityServiceMock;
        private readonly Mock<ILogger<UpvotePostCommandHandler>> _loggerMock;

        private readonly UpvotePostCommandHandler _handler;

        public UpvotePostHandlerTests()
        {
            _postRepositoryMock = new Mock<IGenericRepository<ForumService.Domain.Models.Post>>();
            _voteRepositoryMock = new Mock<IGenericRepository<Vote>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _utilityServiceMock = new Mock<ISUtilityServiceClient>();
            _loggerMock = new Mock<ILogger<UpvotePostCommandHandler>>();

            _handler = new UpvotePostCommandHandler(
                _postRepositoryMock.Object,
                _voteRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _utilityServiceMock.Object,
                _loggerMock.Object
            );
        }

        // Test Case 1: Post Not Found
        [Fact]
        [Trait("Category", "UpvotePostHandler - Validation")]
        public async Task Handle_WhenPostNotFound_Returns404()
        {
            // Arrange
            var command = new UpvotePostCommand(Guid.NewGuid(), Guid.NewGuid());
            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ForumService.Domain.Models.Post)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Post not found.", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 2: User Voting on Own Post (Forbidden)
        [Fact]
        [Trait("Category", "UpvotePostHandler - Validation")]
        public async Task Handle_WhenUserUpvotesOwnPost_Returns403()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var post = new ForumService.Domain.Models.Post { PostId = Guid.NewGuid(), AuthorId = userId, IsDeleted = false };
            var command = new UpvotePostCommand(post.PostId, userId); // Requester == Author

            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.Equal("You cannot vote on your own post.", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 3: New Upvote (Happy Path)
        [Fact]
        [Trait("Category", "UpvotePostHandler - NewVote")]
        public async Task Handle_WhenNoExistingVote_ShouldCreateVoteAndNotify()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var post = new ForumService.Domain.Models.Post
            {
                PostId = Guid.NewGuid(),
                AuthorId = authorId,
                UpvoteCount = 0,
                Title = "Great Post"
            };
            var command = new UpvotePostCommand(post.PostId, requesterId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);

            // Mock GetOneAsync 
            _voteRepositoryMock.Setup(x => x.GetOneAsync(
                    It.IsAny<Expression<Func<Vote, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Vote>, IOrderedQueryable<Vote>>>>(),
                    It.IsAny<Expression<Func<IQueryable<Vote>, IQueryable<Vote>>>>()
                ))
                .ReturnsAsync((Vote)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Post upvoted successfully.", result.Message);
            Assert.Equal(1, post.UpvoteCount); 

            _voteRepositoryMock.Verify(x => x.AddAsync(It.IsAny<Vote>()), Times.Once);
            _postRepositoryMock.Verify(x => x.UpdateAsync(post), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);

            // Verify Notification sent
            _utilityServiceMock.Verify(x => x.SendNotificationAsync(It.Is<NotificationDto>(n => n.UserId == authorId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 4: Toggle Off (Remove Upvote)
        [Fact]
        [Trait("Category", "UpvotePostHandler - ToggleOff")]
        public async Task Handle_WhenAlreadyUpvoted_ShouldRemoveVoteAndNotNotify()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var post = new ForumService.Domain.Models.Post { PostId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), UpvoteCount = 5 };
            var existingVote = new Vote { VoteType = "Upvote", UserId = requesterId, TargetId = post.PostId };
            var command = new UpvotePostCommand(post.PostId, requesterId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);

            _voteRepositoryMock.Setup(x => x.GetOneAsync(
                    It.IsAny<Expression<Func<Vote, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Vote>, IOrderedQueryable<Vote>>>>(),
                    It.IsAny<Expression<Func<IQueryable<Vote>, IQueryable<Vote>>>>()
                ))
                .ReturnsAsync(existingVote);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Upvote removed.", result.Message);
            Assert.Equal(4, post.UpvoteCount); 

            _voteRepositoryMock.Verify(x => x.DeleteAsync(existingVote), Times.Once);

            // Verify Notification IS NOT sent (shouldNotify = false)
            _utilityServiceMock.Verify(x => x.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 5: Switch Vote (Downvote -> Upvote)
        [Fact]
        [Trait("Category", "UpvotePostHandler - SwitchVote")]
        public async Task Handle_WhenDownvoted_ShouldSwitchToUpvoteAndNotify()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var post = new ForumService.Domain.Models.Post
            {
                PostId = Guid.NewGuid(),
                AuthorId = Guid.NewGuid(),
                UpvoteCount = 0,
                DownvoteCount = 1,
                Title = "Controversial Post"
            };
            var existingVote = new Vote { VoteType = "Downvote", UserId = requesterId, TargetId = post.PostId };
            var command = new UpvotePostCommand(post.PostId, requesterId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);
            _voteRepositoryMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Vote, bool>>>(), null, null)).ReturnsAsync(existingVote);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Vote changed to upvote.", result.Message);
            Assert.Equal("Upvote", existingVote.VoteType); 
            Assert.Equal(1, post.UpvoteCount); 
            Assert.Equal(0, post.DownvoteCount);

            _voteRepositoryMock.Verify(x => x.UpdateAsync(existingVote), Times.Once);

            // Notification should be sent
            _utilityServiceMock.Verify(x => x.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 6: Notification Failure (Should not fail request)
        [Fact]
        [Trait("Category", "UpvotePostHandler - NotificationFail")]
        public async Task Handle_WhenNotificationFails_ShouldStillReturnSuccessAndLog()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var post = new ForumService.Domain.Models.Post { PostId = Guid.NewGuid(), AuthorId = Guid.NewGuid(), Title = "Test" };
            var command = new UpvotePostCommand(post.PostId, requesterId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(post.PostId)).ReturnsAsync(post);
            _voteRepositoryMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Vote, bool>>>(), null, null)).ReturnsAsync((Vote)null);

            // Mock Utility Service throw exception
            _utilityServiceMock.Setup(x => x.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Notification Service Down"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status); 
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once); 

            // Verify Logger was called
            
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        // Test Case 7: Database Exception
        [Fact]
        [Trait("Category", "UpvotePostHandler - Exception")]
        public async Task Handle_WhenDbCommitFails_Returns500()
        {
            // Arrange
            var command = new UpvotePostCommand(Guid.NewGuid(), Guid.NewGuid());
            var post = new ForumService.Domain.Models.Post { PostId = Guid.NewGuid(), AuthorId = Guid.NewGuid() };

            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(post);
            _voteRepositoryMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Vote, bool>>>(), null, null)).ReturnsAsync((Vote)null);

            _unitOfWorkMock.Setup(x => x.CommitAsync()).ThrowsAsync(new Exception("DB Error"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }
    }
}
