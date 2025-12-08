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
    public class GetPostDetailsByIdQueryHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Category>> _categoryRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Attachment>> _attachmentRepoMock;
        private readonly Mock<ITagQueryRepository> _tagRepoMock;
        private readonly Mock<IKafkaProducerRepository<User>> _producerRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ILogger<GetPostDetailsByIdQueryHandler>> _loggerMock;

        // SUT
        private readonly GetPostDetailsByIdQueryHandler _handler;

        public GetPostDetailsByIdQueryHandlerTests()
        {
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _categoryRepoMock = new Mock<IGenericRepository<Domain.Models.Category>>();
            _attachmentRepoMock = new Mock<IGenericRepository<Domain.Models.Attachment>>();
            _tagRepoMock = new Mock<ITagQueryRepository>();
            _producerRepoMock = new Mock<IKafkaProducerRepository<User>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _loggerMock = new Mock<ILogger<GetPostDetailsByIdQueryHandler>>();

            _handler = new GetPostDetailsByIdQueryHandler(
                _postRepoMock.Object,
                _commentRepoMock.Object,
                _categoryRepoMock.Object,
                _attachmentRepoMock.Object,
                _tagRepoMock.Object,
                _producerRepoMock.Object,
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );
        }

        // Test Case 1: Post Not Found
        // Scenario: Repo returns null (maybe ID doesn't exist or Status != Published).
        // Expected: Returns 404.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_PostNotFound_ReturnsNotFound()
        {
            // Arrange
            var query = new GetPostDetailsByIdQuery(Guid.NewGuid());

            _postRepoMock.Setup(r => r.GetOneAsync(
                    It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(),
                    null, null))
                .ReturnsAsync((Domain.Models.Post?)null);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Contains("not found or is not published", result.Message);
            Assert.Null(result.ResponseData);
        }

        // Test Case 2: Happy Path - Full Data & Comment Tree
        // Scenario: Post exists, has category, tags, attachments, and nested comments.
        // Expected: 200 OK, View Count incremented, Comment Tree built, Users enriched.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_ReturnsFullDetailsWithNestedComments()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var query = new GetPostDetailsByIdQuery(postId);

            var postEntity = new Domain.Models.Post
            {
                PostId = postId,
                AuthorId = authorId,
                CategoryId = categoryId,
                Title = "Published Post",
                Status = "Published",
                IsDeleted = false,
                ViewsCount = 10
            };
            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(postEntity);

            _categoryRepoMock.Setup(r => r.GetByIdAsync(categoryId))
                .ReturnsAsync(new Domain.Models.Category { Name = "Tech" });

            _attachmentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(), null,
                    It.IsAny<Expression<Func<Domain.Models.Attachment, string>>>(), null, null, null))
                .ReturnsAsync(new List<string> { "img.png" });
            _tagRepoMock.Setup(r => r.GetTagNamesByPostIdAsync(postId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Domain.Models.Tag> { new Domain.Models.Tag { Name = "C#" } });

            var parentId = Guid.NewGuid();
            var childId = Guid.NewGuid();
            var parentAuthorId = Guid.NewGuid();

            var commentDtos = new List<CommentDto>
            {
                new CommentDto { CommentId = parentId, AuthorId = parentAuthorId, Content = "Parent", ParentCommentId = null, Replies = new List<CommentDto>() },
                new CommentDto { CommentId = childId, AuthorId = authorId, Content = "Child", ParentCommentId = parentId, Replies = new List<CommentDto>() }
            };

            _commentRepoMock.Setup(r => r.GetListAsyncUntracked(
                    It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Domain.Models.Comment>, IOrderedQueryable<Domain.Models.Comment>>>>(),
                    It.IsAny<Expression<Func<Domain.Models.Comment, CommentDto>>>(),
                    null, null, null))
                .ReturnsAsync(commentDtos);

            var users = new List<User>
            {
                new User { id = authorId, firstName = "Post", lastName = "Author" },
                new User { id = parentAuthorId, firstName = "Comment", lastName = "Author" }
            };
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(users);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            var data = result.ResponseData;

            Assert.Equal("Published Post", data.Title);
            Assert.Equal("Post", data.AuthorFirstName);
            Assert.Equal("Tech", data.CategoryName);
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _postRepoMock.Verify(r => r.UpdateAsync(postEntity), Times.Once); // Should update view count
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
            Assert.Equal(11, postEntity.ViewsCount); 
            Assert.Single(data.Comments); // Should contain only root comment
            var rootComment = data.Comments.First();
            Assert.Equal(parentId, rootComment.CommentId);
            Assert.Equal("Comment", rootComment.AuthorFirstName); // Enriched
            Assert.Single(rootComment.Replies); // Should contain the child
            Assert.Equal(childId, rootComment.Replies.First().CommentId);
        }

        // Test Case 3: View Count Increment Failure (Resilience)
        // Scenario: Updating view count throws exception (DB lock, etc).
        // Expected: Returns 200 OK (Post details), but logs warning and rolls back the view increment transaction.
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_ViewCountUpdateFails_ContinuesAndReturnsData()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var query = new GetPostDetailsByIdQuery(postId);
            var postEntity = new Domain.Models.Post { PostId = postId, ViewsCount = 0, Status = "Published" };

            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(postEntity);

            // Setup Basic dependencies to return empty
            _tagRepoMock.Setup(r => r.GetTagNamesByPostIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Domain.Models.Tag>());
            _commentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null,
                    It.IsAny<Expression<Func<Domain.Models.Comment, CommentDto>>>(), null, null, null))
                .ReturnsAsync(new List<CommentDto>());
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<User>());
            _attachmentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(), null,
                    It.IsAny<Expression<Func<Domain.Models.Attachment, string>>>(), null, null, null))
                .ReturnsAsync(new List<string>());

            // FAIL the UpdateAsync call
            _postRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Domain.Models.Post>()))
                .ThrowsAsync(new Exception("DB Deadlock"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status); // Still returns success
            Assert.NotNull(result.ResponseData);

            // Verify Error Handling logic
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once); // Rolled back the view count
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to increment view count")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        // Test Case 4: User Service Failure (Resilience)
        // Scenario: Kafka fails.
        // Expected: Returns 200, Post details present, Author fields are null.
        [Fact]
        [Trait("Category", "Handler - Resilience")]
        public async Task Handle_UserServiceFails_ReturnsDataWithoutUserEnrichment()
        {
            // Arrange
            var query = new GetPostDetailsByIdQuery(Guid.NewGuid());
            var postEntity = new Domain.Models.Post { Status = "Published" };

            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(postEntity);

            // Setup basic returns
            _commentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null,
                    It.IsAny<Expression<Func<Domain.Models.Comment, CommentDto>>>(), null, null, null))
                .ReturnsAsync(new List<CommentDto>());
            _attachmentRepoMock.Setup(r => r.GetListAsyncUntracked(It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(), null,
                    It.IsAny<Expression<Func<Domain.Models.Attachment, string>>>(), null, null, null))
                .ReturnsAsync(new List<string>());
            _tagRepoMock.Setup(r => r.GetTagNamesByPostIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Domain.Models.Tag>());

            // FAIL User Service
            _producerRepoMock.Setup(p => p.ProduceGetAllAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Kafka Timeout"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Null(result.ResponseData.AuthorFirstName); // Not enriched

            // Verify logging
            _loggerMock.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to enrich post details")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        // Test Case 5: General Exception
        // Scenario: Initial Post DB call fails.
        // Expected: Returns 500.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_DbException_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetPostDetailsByIdQuery(Guid.NewGuid());

            _postRepoMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ThrowsAsync(new Exception("Critical DB Error"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Equal("An internal server error occurred.", result.Message);

            // Verify Error Log
            _loggerMock.Verify(x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }
    }
}
