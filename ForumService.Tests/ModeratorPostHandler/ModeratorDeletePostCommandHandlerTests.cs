using ForumService.Core.Handler.Post.Command;
using ForumService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;
using ForumService.Contract.TransferObjects;

namespace ForumService.Tests.ModeratorPostHandler
{
    public class ModeratorDeletePostCommandHandlerTests
    {
        // Mocks for dependencies
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _mockPostRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ISUtilityServiceClient> _mockUtilityServiceClient;
        private readonly Mock<ILogger<ModeratorDeletePostCommandHandler>> _mockLogger;
        private readonly Mock<IKafkaProducer> _mockKafkaProducer;
        private readonly Mock<IAppConfiguration> _mockAppConfig;

        // System Under Test
        private readonly ModeratorDeletePostCommandHandler _handler;

        public ModeratorDeletePostCommandHandlerTests()
        {
            // Initialize mocks
            _mockPostRepository = new Mock<IGenericRepository<Domain.Models.Post>>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUtilityServiceClient = new Mock<ISUtilityServiceClient>();
            _mockLogger = new Mock<ILogger<ModeratorDeletePostCommandHandler>>();
            _mockKafkaProducer = new Mock<IKafkaProducer>();
            _mockAppConfig = new Mock<IAppConfiguration>();

            // Setup default behavior for AppConfig to avoid constructor errors
            _mockAppConfig.Setup(x => x.GetCurrentServiceName()).Returns("ForumService");

            // Initialize the handler with mocked dependencies
            _handler = new ModeratorDeletePostCommandHandler(
                _mockPostRepository.Object,
                _mockUnitOfWork.Object,
                _mockUtilityServiceClient.Object,
                _mockLogger.Object,
                _mockKafkaProducer.Object,
                _mockAppConfig.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenPostDoesNotExist()
        {
            // Arrange
            var command = new ModeratorDeletePostCommand(Guid.NewGuid(), Guid.NewGuid(), "Spam");

            // Mock repository to return null (Post not found)
            _mockPostRepository.Setup(repo => repo.GetByIdAsync(command.PostId))
                .ReturnsAsync((Domain.Models.Post)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Post not found or has already been deleted.", result.Message);

            // Verify that no transaction was started
            _mockUnitOfWork.Verify(uow => uow.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenPostIsAlreadyDeleted()
        {
            // Arrange
            var command = new ModeratorDeletePostCommand(Guid.NewGuid(), Guid.NewGuid(), "Spam");
            var existingPost = new Domain.Models.Post
            {
                PostId = command.PostId,
                IsDeleted = true // Post is already deleted
            };

            _mockPostRepository.Setup(repo => repo.GetByIdAsync(command.PostId))
                .ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Post not found or has already been deleted.", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldDeletePostAndReturn200_WhenPostIsValid()
        {
            // Arrange
            var command = new ModeratorDeletePostCommand(Guid.NewGuid(), Guid.NewGuid(), "Inappropriate content");
            var existingPost = new Domain.Models.Post
            {
                PostId = command.PostId,
                AuthorId = command.ModeratorId, // Moderator is the author (no notification needed)
                Status = "Draft", // Not published (no Kafka event needed)
                IsDeleted = false,
                Title = "Test Post"
            };

            _mockPostRepository.Setup(repo => repo.GetByIdAsync(command.PostId))
                .ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);

            // Verify Soft Delete Logic
            Assert.True(existingPost.IsDeleted);
            Assert.NotNull(existingPost.DeletedAt);
            Assert.Equal(command.ModeratorId, existingPost.DeletedBy);
            _mockUnitOfWork.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
            _mockPostRepository.Verify(repo => repo.UpdateAsync(existingPost), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.CommitAsync(), Times.Once);
            _mockKafkaProducer.Verify(k => k.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockUtilityServiceClient.Verify(u => u.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldSendKafkaAndNotification_WhenPostIsPublishedAndAuthorIsDifferent()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid(); // Different from author
            var command = new ModeratorDeletePostCommand(Guid.NewGuid(), moderatorId, "Rule violation");

            var existingPost = new Domain.Models.Post
            {
                PostId = command.PostId,
                AuthorId = authorId,
                Status = "Published", // Triggers Kafka
                IsDeleted = false,
                Title = "Published Post Title"
            };

            _mockPostRepository.Setup(repo => repo.GetByIdAsync(command.PostId))
                .ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);

            // Verify Kafka Event Sent
            _mockKafkaProducer.Verify(k => k.ProduceAsync(
                It.Is<string>(s => s.Contains("user-userstats")), // Topic check
                It.Is<string>(key => key == authorId.ToString()), // Key check
                It.Is<string>(val => val.Contains("POST_DELETED")), // Payload check
                It.IsAny<CancellationToken>()
            ), Times.Once);

            // Verify Notification Sent
            _mockUtilityServiceClient.Verify(u => u.SendNotificationAsync(
                It.Is<NotificationDto>(n => n.UserId == authorId && n.Title.Contains("removed")),
                It.IsAny<CancellationToken>()
            ), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldRollbackAndReturn500_WhenDatabaseUpdateFails()
        {
            // Arrange
            var command = new ModeratorDeletePostCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");
            var existingPost = new Domain.Models.Post { PostId = command.PostId };

            _mockPostRepository.Setup(repo => repo.GetByIdAsync(command.PostId))
                .ReturnsAsync(existingPost);

            // Simulate DB Error on Commit
            _mockUnitOfWork.Setup(uow => uow.CommitAsync())
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("Database connection failed", result.Message);

            // Verify Rollback called
            _mockUnitOfWork.Verify(uow => uow.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn200_EvenIfKafkaProducerFails()
        {
            // Arrange
            var command = new ModeratorDeletePostCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");
            var existingPost = new Domain.Models.Post
            {
                PostId = command.PostId,
                Status = "Published",
                Title = "Test"
            };

            _mockPostRepository.Setup(repo => repo.GetByIdAsync(command.PostId)).ReturnsAsync(existingPost);

            // Simulate Kafka Error
            _mockKafkaProducer.Setup(k => k.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka down"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status); // Should still succeed because Kafka is non-blocking

            // Verify Error was Logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn200_EvenIfNotificationServiceFails()
        {
            // Arrange
            var command = new ModeratorDeletePostCommand(Guid.NewGuid(), Guid.NewGuid(), "Reason");
            var existingPost = new Domain.Models.Post
            {
                PostId = command.PostId,
                AuthorId = Guid.NewGuid(), // Different user to trigger notification
                Status = "Draft",
                Title = "Test"
            };

            _mockPostRepository.Setup(repo => repo.GetByIdAsync(command.PostId)).ReturnsAsync(existingPost);

            // Simulate Notification Service Error
            _mockUtilityServiceClient.Setup(u => u.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Notification service unavailable"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status); // Should still succeed

            // Verify Error was Logged
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)),
                Times.Once);
        }
    }
    }
