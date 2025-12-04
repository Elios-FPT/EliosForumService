using ForumService.Contract.Models;
using ForumService.Core.Handler.Report.Query;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Query;

namespace ForumService.Tests.ReportController
{
    public class GetReportByIdQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Report>> _reportRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IKafkaProducerRepository<User>> _producerRepoMock;

        // System Under Test
        private readonly GetReportByIdQueryHandler _handler;

        public GetReportByIdQueryHandlerTests()
        {
            _reportRepoMock = new Mock<IGenericRepository<Domain.Models.Report>>();
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _producerRepoMock = new Mock<IKafkaProducerRepository<User>>();

            _handler = new GetReportByIdQueryHandler(
                _reportRepoMock.Object,
                _postRepoMock.Object,
                _commentRepoMock.Object,
                _producerRepoMock.Object
            );
        }

        // Test Case 1: Report Not Found
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_ReportNotFound_ReturnsNotFound()
        {
            // Arrange
            var query = new GetReportByIdQuery(Guid.NewGuid());
            _reportRepoMock.Setup(r => r.GetByIdAsync(query.ReportId))
                .ReturnsAsync((Domain.Models.Report?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Report not found.", result.Message);
            Assert.Null(result.ResponseData);
        }

        // Test Case 2: Happy Path - Report on Post with Full User Info
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidPostReport_ReturnsDtoWithEnrichedData()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var resolverId = Guid.NewGuid();
            var targetAuthorId = Guid.NewGuid();

            var report = new Domain.Models.Report
            {
                ReportId = reportId,
                ReporterId = reporterId,
                TargetType = "Post",
                TargetId = targetId,
                ResolvedBy = resolverId,
                Status = "Resolved"
            };

            var post = new Domain.Models.Post
            {
                PostId = targetId,
                AuthorId = targetAuthorId,
                Content = "Short content"
            };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _postRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(post);

            // Mock User Service
            var users = new List<User>
            {
                new User { id = reporterId, firstName = "Reporter", lastName = "User" },
                new User { id = resolverId, firstName = "Admin", lastName = "Mod" },
                new User { id = targetAuthorId, firstName = "Bad", lastName = "Guy" }
            };
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Act
            var result = await _handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            var dto = result.ResponseData;
            Assert.NotNull(dto);

            // Verify Content Mapping
            Assert.Equal("Post", dto.TargetType);
            Assert.Equal("Short content", dto.TargetContentSnippet);

            // Verify User Enrichment
            Assert.Equal("Reporter", dto.ReporterFirstName);
            Assert.Equal("Admin", dto.ResolvedByFirstName);
            Assert.Equal("Bad", dto.TargetAuthorFirstName);
        }

        // Test Case 3: Happy Path - Report on Comment
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidCommentReport_ReturnsDtoWithEnrichedData()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var targetId = Guid.NewGuid();

            var report = new Domain.Models.Report
            {
                ReportId = reportId,
                TargetType = "Comment",
                TargetId = targetId
            };

            var comment = new Domain.Models.Comment
            {
                CommentId = targetId,
                AuthorId = Guid.NewGuid(),
                Content = "Toxic comment"
            };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _commentRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(comment);

            // Mock Empty User List (to verify basic mapping works without users)
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            // Act
            var result = await _handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Comment", result.ResponseData.TargetType);
            Assert.Equal("Toxic comment", result.ResponseData.TargetContentSnippet);
        }

        // Test Case 4: Target Content Deleted (Post)
        [Fact]
        [Trait("Category", "Handler - DataIntegrity")]
        public async Task Handle_TargetPostDeleted_ReturnsDtoWithDeletedMessage()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var report = new Domain.Models.Report { ReportId = reportId, TargetType = "Post", TargetId = targetId };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _postRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync((Domain.Models.Post?)null); // Deleted/Null

            // Act
            var result = await _handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("[Content Deleted or Not Found]", result.ResponseData.TargetContentSnippet);
        }

        // Test Case 5: Long Content Truncation Logic
        [Fact]
        [Trait("Category", "Handler - Logic")]
        public async Task Handle_LongContent_TruncatesSnippet()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var longContent = new string('a', 150); // 150 chars

            var report = new Domain.Models.Report { ReportId = reportId, TargetType = "Post", TargetId = targetId };
            var post = new Domain.Models.Post { PostId = targetId, Content = longContent };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _postRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

            // Assert
            var snippet = result.ResponseData.TargetContentSnippet;
            Assert.EndsWith("...", snippet);
            Assert.Equal(103, snippet.Length); // 100 chars + "..." (3 chars)
        }

        // Test Case 6: Resilience - User Service Failure
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_UserServiceFails_ReturnsDtoWithoutUserInfo()
        {
            // Arrange
            var reportId = Guid.NewGuid();
            var report = new Domain.Models.Report { ReportId = reportId, TargetType = "Post", TargetId = Guid.NewGuid() };
            var post = new Domain.Models.Post { PostId = report.TargetId, Content = "Content" };

            _reportRepoMock.Setup(r => r.GetByIdAsync(reportId)).ReturnsAsync(report);
            _postRepoMock.Setup(r => r.GetByIdAsync(report.TargetId)).ReturnsAsync(post);

            // Simulate Kafka Failure
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka Timeout"));

            // Act
            var result = await _handler.Handle(new GetReportByIdQuery(reportId), CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            // User fields should be null due to try-catch block
            Assert.Null(result.ResponseData.ReporterFirstName);
            Assert.Null(result.ResponseData.TargetAuthorFirstName);
        }
    }
}
