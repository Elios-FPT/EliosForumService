using ForumService.Contract.Models;
using ForumService.Contract.TransferObjects.Comment;
using ForumService.Core.Handler.Post.Query;
using ForumService.Core.Interfaces;
using ForumService.Core.Interfaces.Tag;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Query;

namespace ForumService.Tests.PostHandler
{
    public class GetMyPostByIdQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Attachment>> _attachmentRepoMock;
        private readonly Mock<ITagQueryRepository> _tagRepoMock;
        private readonly Mock<IKafkaProducerRepository<User>> _producerRepoMock;
        private readonly Mock<ILogger<GetMyPostByIdQueryHandler>> _loggerMock;

        // SUT
        private readonly GetMyPostByIdQueryHandler _handler;

        public GetMyPostByIdQueryHandlerTests()
        {
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();
            _attachmentRepoMock = new Mock<IGenericRepository<Domain.Models.Attachment>>();
            _tagRepoMock = new Mock<ITagQueryRepository>();
            _producerRepoMock = new Mock<IKafkaProducerRepository<User>>();
            _loggerMock = new Mock<ILogger<GetMyPostByIdQueryHandler>>();

            _handler = new GetMyPostByIdQueryHandler(
                _postRepoMock.Object,
                _commentRepoMock.Object,
                _categoryRepoMock.Object,
                _attachmentRepoMock.Object,
                _tagRepoMock.Object,
                _producerRepoMock.Object,
                _loggerMock.Object
            );
        }

        // Test Case 1: Post Not Found or Not Owned by Requester
        // Scenario: Repository returns null (filter p.PostId == ID && p.AuthorId == Requester matches nothing)
        // Expected: Returns 404.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_PostNotFoundOrNotOwned_ReturnsNotFound()
        {
            // Arrange
            var query = new GetMyPostByIdQuery(Guid.NewGuid(), Guid.NewGuid());

            _postRepoMock.Setup(r => r.GetOneAsync(
                    It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(),
                    null, null))
                .ReturnsAsync((Domain.Models.Post?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Contains("not found or you do not have permission", result.Message);
            Assert.Null(result.ResponseData);
        }

        // Test Case 2: Happy Path - Full Data
        // Scenario: Post exists, has comments, tags, category, attachments. User service works.
        // Expected: Returns 200 with fully populated DTO.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_ReturnsFullPostDetails()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var query = new GetMyPostByIdQuery(postId, requesterId);

            // 1. Mock Post
            var postEntity = new Domain.Models.Post
            {
                PostId = postId,
                AuthorId = requesterId,
                CategoryId = categoryId,
                Title = "My Draft Post",
                Content = "Content",
                Status = "Draft", // Status shouldn't matter for "My Post"
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };
            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(postEntity);

            // 2. Mock Category
            _categoryRepoMock.Setup(r => r.GetByIdAsync(categoryId))
                .ReturnsAsync(new Domain.Models.Category { Name = "General" });

            // 3. Mock Tags
            _tagRepoMock.Setup(r => r.GetTagNamesByPostIdAsync(postId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Domain.Models.Tag> { new Domain.Models.Tag { Name = "csharp" } });

            // 4. Mock Attachments
            _attachmentRepoMock.Setup(r => r.GetListAsyncUntracked(
                    It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(),
                    null,
                    It.IsAny<Expression<Func<Domain.Models.Attachment, string>>>(), // Selector
                    null, null, null))
                .ReturnsAsync(new List<string> { "http://file.url" });

            // 5. Mock Comments (Flat list with Parent/Child)
            var parentCommentId = Guid.NewGuid();
            var comments = new List<Domain.Models.Comment>
            {
                new Domain.Models.Comment { CommentId = parentCommentId, PostId = postId, AuthorId = requesterId, Content = "Parent" },
                new Domain.Models.Comment { CommentId = Guid.NewGuid(), PostId = postId, ParentCommentId = parentCommentId, AuthorId = Guid.NewGuid(), Content = "Child" }
            };
            // Mocking GetListAsyncUntracked with selector mapping to CommentDto
            // Since the handler uses a projection (Select), we mock the return of the projection directly
            // However, Moq with Expressions and Projections is tricky.
            // Simplified approach: The handler calls GetListAsyncUntracked<CommentDto>.
            // We need to match the Generic Type Argument.
            var commentDtos = comments.Select(c => new CommentDto
            {
                CommentId = c.CommentId,
                AuthorId = c.AuthorId,
                ParentCommentId = c.ParentCommentId,
                Content = c.Content
            }).ToList();

            _commentRepoMock.Setup(r => r.GetListAsyncUntracked(
                    It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Comment>, IOrderedQueryable<Domain.Models.Comment>>>>(),
                    It.IsAny<Expression<Func<Domain.Models.Comment, CommentDto>>>(), // Selector
                    null, null, null))
                .ReturnsAsync(commentDtos);

            // 6. Mock User Service (Kafka)
            var users = new List<User>
            {
                new User { id = requesterId, firstName = "Me", lastName = "Myself" }
            };
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            var data = result.ResponseData;
            Assert.NotNull(data);
            Assert.Equal("My Draft Post", data.Title);
            Assert.Equal("Me", data.AuthorFirstName); // User enriched
            Assert.Equal("General", data.CategoryName); // Category enriched
            Assert.Single(data.Tags); // Tags enriched
            Assert.Single(data.Url); // Attachments enriched

            // Verify Comment Tree
            Assert.Single(data.Comments); // Should have 1 root comment
            Assert.Single(data.Comments.First().Replies); // Root comment should have 1 reply
        }

        // Test Case 3: Resilience - User Service Failure
        // Scenario: Kafka throws exception.
        // Expected: Returns 200, Post data present, Author info null.
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_UserServiceFails_ReturnsPostWithoutAuthorDetails()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var query = new GetMyPostByIdQuery(postId, requesterId);

            var postEntity = new Domain.Models.Post { PostId = postId, AuthorId = requesterId, Title = "Post" };

            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(postEntity);

            // Setup basic dependencies to return empty/null to isolate User Service check
            _tagRepoMock.Setup(r => r.GetTagNamesByPostIdAsync(postId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Domain.Models.Tag>());
            _commentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Comment>, IOrderedQueryable<Domain.Models.Comment>>>>(),
                    It.IsAny<Expression<Func<Domain.Models.Comment, CommentDto>>>(), null, null, null))
                .ReturnsAsync(new List<CommentDto>());
            _attachmentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(), null,
                    It.IsAny<Expression<Func<Domain.Models.Attachment, string>>>(), null, null, null))
                .ReturnsAsync(new List<string>());

            // FAIL User Service
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka down"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Post", result.ResponseData.Title);
            Assert.Null(result.ResponseData.AuthorFirstName); // Should be null due to failure

            // Verify Logger was called
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to enrich post details")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        // Test Case 4: Database Exception
        // Scenario: Post Repository throws.
        // Expected: Returns 500.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DbException_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetMyPostByIdQuery(Guid.NewGuid(), Guid.NewGuid());

            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ThrowsAsync(new Exception("DB Connection Timeout"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Equal("An internal server error occurred.", result.Message);
        }

        // Test Case 5: Logic - Minimal Data (Null Category, No Tags/Comments)
        // Scenario: Post has no optional relationships.
        // Expected: 200 OK, null/empty optional fields.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_MinimalPost_ReturnsSuccess()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var query = new GetMyPostByIdQuery(postId, requesterId);

            var postEntity = new Domain.Models.Post
            {
                PostId = postId,
                AuthorId = requesterId,
                CategoryId = null // No Category
            };

            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(postEntity);

            // Empty returns for others
            _tagRepoMock.Setup(r => r.GetTagNamesByPostIdAsync(postId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Domain.Models.Tag>());
            _commentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Comment>, IOrderedQueryable<Domain.Models.Comment>>>>(),
                    It.IsAny<Expression<Func<Domain.Models.Comment, CommentDto>>>(), null, null, null))
                .ReturnsAsync(new List<CommentDto>());
            _attachmentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(), null,
                   It.IsAny<Expression<Func<Domain.Models.Attachment, string>>>(), null, null, null))
               .ReturnsAsync(new List<string>());

            // Mock User Service success (empty list is fine)
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Null(result.ResponseData.CategoryName);
            Assert.Empty(result.ResponseData.Tags);
            Assert.Empty(result.ResponseData.Comments);
        }
    }
}
