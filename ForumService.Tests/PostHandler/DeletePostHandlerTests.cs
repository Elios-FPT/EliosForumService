using ForumService.Contract.Message;
using ForumService.Contract.Shared;
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

namespace ForumService.Tests.PostHandler
{
    public class DeletePostHandlerTests
    {
        private readonly Mock<IGenericRepository<ForumService.Domain.Models.Post>> _postRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IKafkaProducer> _kafkaProducerMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ILogger<DeletePostCommandHandler>> _loggerMock;
        // Class cần test
        private readonly DeletePostCommandHandler _handler;

        public DeletePostHandlerTests()
        {
            _postRepositoryMock = new Mock<IGenericRepository<ForumService.Domain.Models.Post>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _kafkaProducerMock = new Mock<IKafkaProducer>();
            _appConfigMock = new Mock<IAppConfiguration>();
            _loggerMock = new Mock<ILogger<DeletePostCommandHandler>>();

            _handler = new DeletePostCommandHandler(
                _postRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _kafkaProducerMock.Object, 
                _appConfigMock.Object,    
                _loggerMock.Object         
            );
        }

        [Fact]
        [Trait("Category", "DeletePostHandler - HappyPath")]
        public async Task Handle_WhenPostExistsAndUserIsAuthor_ShouldSoftDeleteAndCommit()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var existingPost = new ForumService.Domain.Models.Post
            {
                PostId = postId,
                AuthorId = userId,
                IsDeleted = false
            };

            var command = new DeletePostCommand(postId, userId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(postId))
                               .ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post deleted successfully.", result.Message);

            Assert.True(existingPost.IsDeleted);
            Assert.Equal(userId, existingPost.DeletedBy);
            Assert.NotNull(existingPost.DeletedAt);

            _postRepositoryMock.Verify(x => x.UpdateAsync(It.Is<ForumService.Domain.Models.Post>(p => p.IsDeleted == true && p.PostId == postId)), Times.Once);

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Never);
        }

        // Test Case 2: Post Not Found (Null)
        [Fact]
        [Trait("Category", "DeletePostHandler - NotFound")]
        public async Task Handle_WhenPostDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var command = new DeletePostCommand(postId, userId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(postId))
                               .ReturnsAsync((ForumService.Domain.Models.Post)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Post not found.", result.Message);

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 3: Post Not Found 
        [Fact]
        [Trait("Category", "DeletePostHandler - NotFound")]
        public async Task Handle_WhenPostIsAlreadyDeleted_ReturnsNotFound()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var command = new DeletePostCommand(postId, userId);

            var deletedPost = new ForumService.Domain.Models.Post
            {
                PostId = postId,
                AuthorId = userId,
                IsDeleted = true
            };

            _postRepositoryMock.Setup(x => x.GetByIdAsync(postId))
                               .ReturnsAsync(deletedPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status); 
            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 4: Forbidden
        [Fact]
        [Trait("Category", "DeletePostHandler - Forbidden")]
        public async Task Handle_WhenUserIsNotAuthor_ReturnsForbidden()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var requesterId = Guid.NewGuid(); 

            var existingPost = new ForumService.Domain.Models.Post
            {
                PostId = postId,
                AuthorId = authorId,
                IsDeleted = false
            };

            var command = new DeletePostCommand(postId, requesterId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(postId))
                               .ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("You are not authorized to delete this post.", result.Message);

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 5: Exception Handling (Rollback)
        [Fact]
        [Trait("Category", "DeletePostHandler - Exception")]
        public async Task Handle_WhenExceptionOccurs_ShouldRollbackAndReturn500()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            var existingPost = new ForumService.Domain.Models.Post
            {
                PostId = postId,
                AuthorId = userId,
                IsDeleted = false
            };

            var command = new DeletePostCommand(postId, userId);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(postId))
                               .ReturnsAsync(existingPost);

            _unitOfWorkMock.Setup(x => x.CommitAsync())
                           .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("Database connection failed", result.Message);

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once); 
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once); 
        }
    }
}