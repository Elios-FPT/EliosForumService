using ForumService.Core.Handler.Report.Command;
using ForumService.Core.Interfaces;
using Moq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Command;

namespace ForumService.Tests.ReportController
{
    public class CreateReportCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Report>> _reportRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        // System Under Test
        private readonly CreateReportCommandHandler _handler;

        public CreateReportCommandHandlerTests()
        {
            _reportRepoMock = new Mock<IGenericRepository<Domain.Models.Report>>();
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new CreateReportCommandHandler(
                _reportRepoMock.Object,
                _postRepoMock.Object,
                _commentRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        // Test Case 1: Validation - Empty Reason
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_EmptyReason_ReturnsBadRequest()
        {
            // Arrange
            var command = new CreateReportCommand(Guid.NewGuid(), "Post", Guid.NewGuid(), "", null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Reason cannot be empty.", result.Message);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 2: Validation - Invalid TargetType
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_InvalidTargetType_ReturnsBadRequest()
        {
            // Arrange
            var command = new CreateReportCommand(Guid.NewGuid(), "User", Guid.NewGuid(), "Spam", null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("TargetType must be 'Post' or 'Comment'", result.Message);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 3: Target Not Found (Post)
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_PostNotFound_ReturnsNotFound()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var command = new CreateReportCommand(Guid.NewGuid(), "Post", targetId, "Spam", null);

            _postRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync((Domain.Models.Post?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Post not found.", result.Message);

            // Verify Rollback
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 4: Target Not Found (Comment)
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_CommentNotFound_ReturnsNotFound()
        {
            // Arrange
            var targetId = Guid.NewGuid();
            var command = new CreateReportCommand(Guid.NewGuid(), "Comment", targetId, "Toxic", null);

            _commentRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync((Domain.Models.Comment?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Comment not found.", result.Message);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 5: Self Reporting (Forbidden)
        [Fact]
        [Trait("Category", "Handler - Authorization")]
        public async Task Handle_SelfReporting_ReturnsForbidden()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            // ReporterId == AuthorId
            var command = new CreateReportCommand(userId, "Post", targetId, "Spam", null);

            var post = new Domain.Models.Post { PostId = targetId, AuthorId = userId };

            _postRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.Equal("You cannot report your own content.", result.Message);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 6: Duplicate Pending Report (Conflict)
        [Fact]
        [Trait("Category", "Handler - BusinessRule")]
        public async Task Handle_DuplicatePendingReport_ReturnsConflict()
        {
            // Arrange
            var reporterId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var command = new CreateReportCommand(reporterId, "Post", targetId, "Spam", null);

            var post = new Domain.Models.Post { PostId = targetId, AuthorId = Guid.NewGuid() };
            var existingReport = new Domain.Models.Report { ReportId = Guid.NewGuid() };

            _postRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(post);

            // Mock finding an existing pending report
            _reportRepoMock.Setup(r => r.GetOneAsync(
                It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(), null, null))
                .ReturnsAsync(existingReport);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(409, result.Status);
            Assert.Contains("already submitted a pending report", result.Message);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 7: Happy Path - Success (Post)
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidPostReport_ReturnsCreated()
        {
            // Arrange
            var reporterId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var command = new CreateReportCommand(reporterId, "Post", targetId, "Spam", "Details");

            var post = new Domain.Models.Post { PostId = targetId, AuthorId = Guid.NewGuid() }; // Different author

            _postRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(post);

            // No duplicate report
            _reportRepoMock.Setup(r => r.GetOneAsync(
                It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(), null, null))
                .ReturnsAsync((Domain.Models.Report?)null);

            // Capture the entity to check properties
            Domain.Models.Report capturedReport = null;
            _reportRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Report>()))
                .Callback<Domain.Models.Report>(r => capturedReport = r)
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(201, result.Status);
            Assert.Equal("Report submitted successfully.", result.Message);
            Assert.NotEqual(Guid.Empty, result.ResponseData);

            Assert.NotNull(capturedReport);
            Assert.Equal(reporterId, capturedReport.ReporterId);
            Assert.Equal("Post", capturedReport.TargetType);
            Assert.Equal("Pending", capturedReport.Status);

            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        // Test Case 8: Exception Handling
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DbError_RollsBackTransaction()
        {
            // Arrange
            var command = new CreateReportCommand(Guid.NewGuid(), "Post", Guid.NewGuid(), "Spam", null);
            var post = new Domain.Models.Post { AuthorId = Guid.NewGuid() };

            _postRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(post);
            _reportRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(), null, null))
                .ReturnsAsync((Domain.Models.Report?)null);

            // Simulate DB Error on Add
            _reportRepoMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Report>()))
                .ThrowsAsync(new Exception("Database disconnected"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Database disconnected", result.Message);

            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}
