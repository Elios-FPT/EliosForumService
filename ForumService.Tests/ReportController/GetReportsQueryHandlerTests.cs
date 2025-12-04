using ForumService.Contract.Models;
using ForumService.Core.Handler.Report.Query;
using ForumService.Core.Interfaces;
using Moq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Report.Query;

namespace ForumService.Tests.ReportController
{
    public class GetReportsQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Report>> _reportRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IKafkaProducerRepository<User>> _producerRepoMock;

        // System Under Test
        private readonly GetReportsQueryHandler _handler;

        public GetReportsQueryHandlerTests()
        {
            _reportRepoMock = new Mock<IGenericRepository<Domain.Models.Report>>();
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _producerRepoMock = new Mock<IKafkaProducerRepository<User>>();

            _handler = new GetReportsQueryHandler(
                _reportRepoMock.Object,
                _postRepoMock.Object,
                _commentRepoMock.Object,
                _producerRepoMock.Object
            );
        }

        // Test Case 1: Happy Path - Get List with Full Enrichment
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_ReturnsEnrichedReports()
        {
            // Arrange
            var query = new GetReportsQuery(1, 10, null, null, null, null, null, null);
            var reporterId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var commentId = Guid.NewGuid();

            var reports = new List<Domain.Models.Report>
            {
                new Domain.Models.Report { ReportId = Guid.NewGuid(), TargetType = "Post", TargetId = postId, ReporterId = reporterId, CreatedAt = DateTime.UtcNow },
                new Domain.Models.Report { ReportId = Guid.NewGuid(), TargetType = "Comment", TargetId = commentId, ReporterId = reporterId, CreatedAt = DateTime.UtcNow }
            };

            // Mock Report Repo
            _reportRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Report, bool>>>()))
                .ReturnsAsync(2);
            _reportRepoMock.Setup(r => r.GetListAsync(
                    It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Report>, IOrderedQueryable<Domain.Models.Report>>>>(),
                    null, 10, 1))
                .ReturnsAsync(reports);

            // Mock Content Repos
            var posts = new List<Domain.Models.Post> { new Domain.Models.Post { PostId = postId, Content = "Post Content", AuthorId = Guid.NewGuid() } };
            var comments = new List<Domain.Models.Comment> { new Domain.Models.Comment { CommentId = commentId, Content = "Comment Content", AuthorId = Guid.NewGuid() } };

            _postRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null, null, null))
                .ReturnsAsync(posts);
            _commentRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null, null, null))
                .ReturnsAsync(comments);

            // Mock User Service
            var users = new List<User>
            {
                new User { id = reporterId, firstName = "John", lastName = "Doe" }
            };
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal(2, result.ResponseData.Count());

            var postReport = result.ResponseData.First(r => r.TargetType == "Post");
            Assert.Equal("Post Content", postReport.TargetContentDetail);
            Assert.Equal("John", postReport.ReporterFirstName);

            var commentReport = result.ResponseData.First(r => r.TargetType == "Comment");
            Assert.Equal("Comment Content", commentReport.TargetContentDetail);
        }

        // Test Case 2: Empty State
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_NoReportsFound_ReturnsEmptyList()
        {
            // Arrange
            var query = new GetReportsQuery(1, 10, "Pending", null, null, null, null, null);

            _reportRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Report, bool>>>()))
                .ReturnsAsync(0);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No reports found.", result.Message);

            // Ensure GetListAsync is skipped optimization
            _reportRepoMock.Verify(r => r.GetListAsync(
                It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(),
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Report>, IOrderedQueryable<Domain.Models.Report>>>>(),
                null, 10, 1), Times.Never);
        }

        // Test Case 3: Sorting Logic
        [Fact]
        [Trait("Category", "Handler - Logic")]
        public async Task Handle_SortingRequest_PassesOrderByToRepo()
        {
            // Arrange
            var query = new GetReportsQuery(1, 10, null, null, null, null, "Status", "ASC");

            _reportRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Report, bool>>>()))
                .ReturnsAsync(1);

            _reportRepoMock.Setup(r => r.GetListAsync(
                    It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(),
                    It.IsNotNull<Expression<Func<IQueryable<Domain.Models.Report>, IOrderedQueryable<Domain.Models.Report>>>>(), // Check OrderBy is passed
                    null, 10, 1))
                .ReturnsAsync(new List<Domain.Models.Report> { new Domain.Models.Report() });

            // Setup dependencies to return empty/defaults
            _postRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<Domain.Models.Post>());
            _commentRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<Domain.Models.Comment>());
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            _reportRepoMock.Verify(r => r.GetListAsync(
                    It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(),
                    It.IsNotNull<Expression<Func<IQueryable<Domain.Models.Report>, IOrderedQueryable<Domain.Models.Report>>>>(),
                    null, 10, 1),
                Times.Once);
        }

        // Test Case 4: Resilience - User Service Fails
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_UserServiceFails_ReturnsReportsWithoutUserInfo()
        {
            // Arrange
            var query = new GetReportsQuery(1, 10, null, null, null, null, null, null);
            var report = new Domain.Models.Report { ReportId = Guid.NewGuid(), ReporterId = Guid.NewGuid() };

            _reportRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Report, bool>>>()))
                .ReturnsAsync(1);
            _reportRepoMock.Setup(r => r.GetListAsync(
                    It.IsAny<Expression<Func<Domain.Models.Report, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Report>, IOrderedQueryable<Domain.Models.Report>>>>(),
                    null, 10, 1))
                .ReturnsAsync(new List<Domain.Models.Report> { report });

            // Mock Empty Post/Comment to isolate User Service test
            _postRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<Domain.Models.Post>());
            _commentRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<Domain.Models.Comment>());

            // Fail Kafka
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka Down"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Single(result.ResponseData);
            var dto = result.ResponseData.First();
            Assert.Equal(report.ReportId, dto.ReportId);
            Assert.Null(dto.ReporterFirstName); // Should be null due to failure
        }

        // Test Case 5: Exception - DB Error
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DbError_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetReportsQuery(1, 10, null, null, null, null, null, null);

            _reportRepoMock.Setup(r => r.GetCountAsync(It.IsAny<Expression<Func<Domain.Models.Report, bool>>>()))
                .ThrowsAsync(new Exception("DB Connection Lost"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("An error occurred", result.Message);
            Assert.Empty(result.ResponseData);
        }
    }
}
