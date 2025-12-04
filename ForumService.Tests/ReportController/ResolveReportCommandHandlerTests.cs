using ForumService.Core.Handler.Report.Command;
using ForumService.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Command;
using ForumService.Contract.TransferObjects;

namespace ForumService.Tests.ReportController
{
    public class ResolveReportCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Report>> _reportRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ISUtilityServiceClient> _utilityServiceMock;
        private readonly Mock<ILogger<ResolveReportCommandHandler>> _loggerMock;
        private readonly Mock<IKafkaProducer> _kafkaProducerMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;

        // System Under Test
        private readonly ResolveReportCommandHandler _handler;

        public ResolveReportCommandHandlerTests()
        {
            _reportRepoMock = new Mock<IGenericRepository<Domain.Models.Report>>();
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _utilityServiceMock = new Mock<ISUtilityServiceClient>();
            _loggerMock = new Mock<ILogger<ResolveReportCommandHandler>>();
            _kafkaProducerMock = new Mock<IKafkaProducer>();
            _appConfigMock = new Mock<IAppConfiguration>();

            // Setup AppConfig default behavior
            _appConfigMock.Setup(c => c.GetCurrentServiceName()).Returns("forum-service");

            _handler = new ResolveReportCommandHandler(
                _reportRepoMock.Object,
                _postRepoMock.Object,
                _commentRepoMock.Object,
                _unitOfWorkMock.Object,
                _utilityServiceMock.Object,
                _loggerMock.Object,
                _kafkaProducerMock.Object,
                _appConfigMock.Object
            );
        }

        // Test Case 1: Invalid Status Input
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_InvalidStatus_ReturnsBadRequest()
        {
            // Arrange
            var command = new ResolveReportCommand(Guid.NewGuid(), Guid.NewGuid(), "InvalidStatus", false, "Note");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Status must be 'Approved' or 'Rejected'.", result.Message);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 2: Report Not Found
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_ReportNotFound_ReturnsNotFound()
        {
            // Arrange
            var command = new ResolveReportCommand(Guid.NewGuid(), Guid.NewGuid(), "Approved", false, null);
            _reportRepoMock.Setup(r => r.GetByIdAsync(command.ReportId))
                .ReturnsAsync((Domain.Models.Report?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 3: Report Already Processed
        [Fact]
        [Trait("Category", "Handler - BusinessRule")]
        public async Task Handle_ReportAlreadyResolved_ReturnsBadRequest()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var command = new ResolveReportCommand(reportId, Guid.NewGuid(), "Rejected", false, null);
            var report = new Domain.Models.Report { ReportId = reportId, Status = "Resolved" };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("already been processed", result.Message);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 4: Happy Path - Reject Report (No Action on Content)
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_RejectReport_UpdatesReportOnly()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var command = new ResolveReportCommand(reportId, moderatorId, "Rejected", false, "False claim");

            var report = new Domain.Models.Report { ReportId = reportId, Status = "Pending", TargetType = "Post" };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Report rejected.", result.Message);

            // Verify Report Updated
            Assert.Equal("Rejected", report.Status);
            Assert.Equal(moderatorId, report.ResolvedBy);
            Assert.Equal("False claim", report.ModeratorNote);
            Assert.NotNull(report.ResolvedAt);

            // Verify Content Repos NOT touched for update
            _postRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Domain.Models.Post>()), Times.Never);
            _commentRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Domain.Models.Comment>()), Times.Never);

            // Verify Commit
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        // Test Case 5: Happy Path - Approve Report & Delete Post & Send Kafka
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ApproveAndDeletePost_DeletesContentAndSendsKafka()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var postAuthorId = Guid.NewGuid();
            var command = new ResolveReportCommand(reportId, Guid.NewGuid(), "Approved", true, "Violates terms");

            var report = new Domain.Models.Report
            {
                ReportId = reportId,
                Status = "Pending",
                TargetType = "Post",
                TargetId = postId
            };

            var post = new Domain.Models.Post
            {
                PostId = postId,
                AuthorId = postAuthorId,
                Status = "Published", // Published triggers Kafka
                IsDeleted = false
            };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _postRepoMock.Setup(r => r.GetByIdAsync(postId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Contains("content deleted", result.Message);

            // Verify Post Deleted
            Assert.True(post.IsDeleted);
            Assert.NotNull(post.DeletedAt);
            _postRepoMock.Verify(r => r.UpdateAsync(post), Times.Once);

            // Verify Kafka Event Sent (because post was Published)
            _kafkaProducerMock.Verify(k => k.ProduceAsync(
                It.Is<string>(s => s.Contains("user-userstats")),
                postAuthorId.ToString(),
                It.Is<string>(json => json.Contains("POST_DELETED")),
                It.IsAny<CancellationToken>()
            ), Times.Once);

            // Verify Notification Sent
            _utilityServiceMock.Verify(u => u.SendNotificationAsync(It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 6: Happy Path - Approve & Delete Comment
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ApproveAndDeleteComment_DeletesContent()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var command = new ResolveReportCommand(reportId, Guid.NewGuid(), "Approved", true, null);

            var report = new Domain.Models.Report
            {
                ReportId = reportId,
                Status = "Pending",
                TargetType = "Comment",
                TargetId = commentId
            };

            var comment = new Domain.Models.Comment
            {
                CommentId = commentId,
                IsDeleted = false
            };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _commentRepoMock.Setup(r => r.GetByIdAsync(commentId)).ReturnsAsync(comment);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);

            // Verify Comment Deleted
            Assert.True(comment.IsDeleted);
            _commentRepoMock.Verify(r => r.UpdateAsync(comment), Times.Once);

            // Verify Post Repo NOT touched
            _postRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Domain.Models.Post>()), Times.Never);
        }

        // Test Case 7: Resilience - Kafka Failure
        // Scenario: Post deleted successfully, but Kafka Producer throws exception.
        // Expected: Handler returns 200 (Transaction committed), error is just logged.
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_KafkaFails_StillReturnsSuccessAndLogsError()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new ResolveReportCommand(reportId, Guid.NewGuid(), "Approved", true, null);

            var report = new Domain.Models.Report { ReportId = reportId, Status = "Pending", TargetType = "Post", TargetId = postId };
            var post = new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _postRepoMock.Setup(r => r.GetByIdAsync(postId)).ReturnsAsync(post);

            // Simulate Kafka Failure
            _kafkaProducerMock.Setup(k => k.ProduceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka Down"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status); // Main transaction successful
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once); // DB Committed

            // Verify Logger called for Kafka error
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("failed to send stats event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        // Test Case 8: Exception - DB Error triggers Rollback
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DbUpdateFails_RollsBackTransaction()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var command = new ResolveReportCommand(reportId, Guid.NewGuid(), "Rejected", false, null);
            var report = new Domain.Models.Report { ReportId = reportId, Status = "Pending" };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);

            // Simulate DB Failure on Update
            _reportRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Domain.Models.Report>()))
                .ThrowsAsync(new Exception("DB Connection Timeout"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Error resolving report", result.Message);

            // Verify Rollback
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}
