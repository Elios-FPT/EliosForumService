using ForumService.Core.Handler.Upload;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Upload.Command;

namespace ForumService.Tests.UploadHandler
{
    public class DeleteAttachmentCommandHandlerTests
    {
        // Mocks for dependencies
        private readonly Mock<IGenericRepository<Domain.Models.Attachment>> _mockAttachmentRepository;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _mockPostRepository;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;

        // System Under Test
        private readonly DeleteAttachmentCommandHandler _handler;

        public DeleteAttachmentCommandHandlerTests()
        {
            _mockAttachmentRepository = new Mock<IGenericRepository<Domain.Models.Attachment>>();
            _mockPostRepository = new Mock<IGenericRepository<Domain.Models.Post>>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();

            _handler = new DeleteAttachmentCommandHandler(
                _mockAttachmentRepository.Object,
                _mockPostRepository.Object,
                _mockUnitOfWork.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenAttachmentDoesNotExist()
        {
            // Arrange
            var command = new DeleteAttachmentCommand(Guid.NewGuid(), Guid.NewGuid());

            // Mock Repo to return null
            _mockAttachmentRepository.Setup(repo => repo.GetByIdAsync(command.AttachmentId))
                .ReturnsAsync((Domain.Models.Attachment)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Attachment not found.", result.Message);

            // Verify no delete action occurred
            _mockAttachmentRepository.Verify(repo => repo.DeleteAsync(It.IsAny<Domain.Models.Attachment>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturn403_WhenUserIsNotTheUploader()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var requesterId = Guid.NewGuid(); // Different ID
            var command = new DeleteAttachmentCommand(Guid.NewGuid(), requesterId);

            var attachment = new Domain.Models.Attachment
            {
                AttachmentId = command.AttachmentId,
                UploadedBy = ownerId // Owned by someone else
            };

            _mockAttachmentRepository.Setup(repo => repo.GetByIdAsync(command.AttachmentId))
                .ReturnsAsync(attachment);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.Equal("You are not authorized to delete this attachment.", result.Message);

            _mockAttachmentRepository.Verify(repo => repo.DeleteAsync(It.IsAny<Domain.Models.Attachment>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenAttachmentIsLinkedToPublishedPost()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new DeleteAttachmentCommand(Guid.NewGuid(), userId);

            var attachment = new Domain.Models.Attachment
            {
                AttachmentId = command.AttachmentId,
                UploadedBy = userId,
                TargetType = "Post",
                TargetId = postId
            };

            var post = new Domain.Models.Post
            {
                PostId = postId,
                Status = "Published",
                IsDeleted = false
            };

            _mockAttachmentRepository.Setup(repo => repo.GetByIdAsync(command.AttachmentId))
                .ReturnsAsync(attachment);

            _mockPostRepository.Setup(repo => repo.GetByIdAsync(postId))
                .ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("Cannot delete this attachment because it is used in a published post", result.Message);
            Assert.False(result.ResponseData);

            _mockAttachmentRepository.Verify(repo => repo.DeleteAsync(It.IsAny<Domain.Models.Attachment>()), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturn200_WhenAttachmentIsLinkedToDraftPost()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new DeleteAttachmentCommand(Guid.NewGuid(), userId);

            var attachment = new Domain.Models.Attachment
            {
                AttachmentId = command.AttachmentId,
                UploadedBy = userId,
                TargetType = "Post",
                TargetId = postId
            };

            // Post is Draft (Should allow deletion)
            var post = new Domain.Models.Post
            {
                PostId = postId,
                Status = "Draft",
                IsDeleted = false
            };

            _mockAttachmentRepository.Setup(repo => repo.GetByIdAsync(command.AttachmentId)).ReturnsAsync(attachment);
            _mockPostRepository.Setup(repo => repo.GetByIdAsync(postId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Attachment deleted successfully.", result.Message);

            // Verify Delete and SaveChanges were called
            _mockAttachmentRepository.Verify(repo => repo.DeleteAsync(attachment), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn200_WhenAttachmentHasNoTarget()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new DeleteAttachmentCommand(Guid.NewGuid(), userId);

            var attachment = new Domain.Models.Attachment
            {
                AttachmentId = command.AttachmentId,
                UploadedBy = userId,
                TargetType = null, // Not linked to anything
                TargetId = null
            };

            _mockAttachmentRepository.Setup(repo => repo.GetByIdAsync(command.AttachmentId)).ReturnsAsync(attachment);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);

            // Post repo should NOT be called since TargetType is null
            _mockPostRepository.Verify(repo => repo.GetByIdAsync(It.IsAny<Guid>()), Times.Never);

            _mockAttachmentRepository.Verify(repo => repo.DeleteAsync(attachment), Times.Once);
            _mockUnitOfWork.Verify(uow => uow.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenDatabaseThrowsException()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new DeleteAttachmentCommand(Guid.NewGuid(), userId);
            var attachment = new Domain.Models.Attachment { AttachmentId = command.AttachmentId, UploadedBy = userId };

            _mockAttachmentRepository.Setup(repo => repo.GetByIdAsync(command.AttachmentId)).ReturnsAsync(attachment);

            // Simulate DB Error on SaveChanges
            _mockUnitOfWork.Setup(uow => uow.SaveChangesAsync())
                .ThrowsAsync(new Exception("Database connection error"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Failed to delete attachment", result.Message);
            Assert.False(result.ResponseData);
        }
    }
}
