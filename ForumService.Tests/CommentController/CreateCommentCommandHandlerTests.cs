using ForumService.Contract.Shared;
using ForumService.Core.Handler.Comment.Command;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Moq;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Tests.Handler.Comment.Command
{
    public class CreateCommentCommandHandlerTests
    {
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepositoryMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly CreateCommentCommandHandler _handler;

        public CreateCommentCommandHandlerTests()
        {
            _commentRepositoryMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _postRepositoryMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _handler = new CreateCommentCommandHandler(
                _commentRepositoryMock.Object,
                _postRepositoryMock.Object,
                _unitOfWorkMock.Object);
        }

        [Fact]
        [Trait("Category", "CreateComment - HappyPath")]
        public async Task Handle_ValidRootComment_CreatesCommentAndReturnsId()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, null, authorId, "Test content");
            var validPost = new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false };
            Guid? createdCommentId = null;

            _postRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                               .ReturnsAsync(validPost);
            _commentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Comment>()))
                                  .Callback<Domain.Models.Comment>(c => createdCommentId = c.CommentId) 
                                  .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(201, result.Status);
            Assert.Equal("Comment created successfully.", result.Message);
            Assert.NotEqual(Guid.Empty, result.ResponseData);
            Assert.Equal(createdCommentId, result.ResponseData); 
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
            _commentRepositoryMock.Verify(r => r.AddAsync(It.Is<Domain.Models.Comment>(c =>
                c.PostId == postId &&
                c.ParentCommentId == null &&
                c.AuthorId == authorId &&
                c.Content == "Test content")), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.RollbackAsync(), Times.Never);
        }

        // Test Case 2: Happy Path - Create Reply Comment
        [Fact]
        [Trait("Category", "CreateComment - HappyPath")]
        public async Task Handle_ValidReplyComment_CreatesCommentAndReturnsId()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, parentCommentId, authorId, "Reply content");
            var validPost = new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false };
            var validParentComment = new Domain.Models.Comment { CommentId = parentCommentId, PostId = postId, IsDeleted = false };

            _postRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                               .ReturnsAsync(validPost);
            _commentRepositoryMock.Setup(r => r.GetOneAsync(It.Is<Expression<Func<Domain.Models.Comment, bool>>>(
                                   expr => expr.Compile()(new Domain.Models.Comment { CommentId = parentCommentId, IsDeleted = false })), null, null)) // Mock specific GetOneAsync for parent comment
                               .ReturnsAsync(validParentComment);
            _commentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Comment>()))
                                  .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(201, result.Status);
            Assert.NotEqual(Guid.Empty, result.ResponseData);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
            _commentRepositoryMock.Verify(r => r.AddAsync(It.Is<Domain.Models.Comment>(c => c.ParentCommentId == parentCommentId)), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.RollbackAsync(), Times.Never);
        }

        // Test Case 3: Validation Failure - Empty AuthorId
        [Fact]
        [Trait("Category", "CreateComment - ValidationFailure")]
        public async Task Handle_EmptyAuthorId_ReturnsBadRequest()
        {
            // Arrange
            var command = new CreateCommentCommand(Guid.NewGuid(), null, Guid.Empty, "Content");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("AuthorId cannot be empty.", result.Message);
            Assert.Equal(Guid.Empty, result.ResponseData);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Never); // Should not start transaction
        }

        // Test Case 4: Validation Failure - Empty PostId
        [Fact]
        [Trait("Category", "CreateComment - ValidationFailure")]
        public async Task Handle_EmptyPostId_ReturnsBadRequest()
        {
            // Arrange
            var command = new CreateCommentCommand(Guid.Empty, null, Guid.NewGuid(), "Content");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("PostId cannot be empty.", result.Message);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Never);
        }


        // Test Case 5: Validation Failure - Empty Content
        [Fact]
        [Trait("Category", "CreateComment - ValidationFailure")]
        public async Task Handle_EmptyContent_ReturnsBadRequest()
        {
            // Arrange
            var command = new CreateCommentCommand(Guid.NewGuid(), null, Guid.NewGuid(), " "); // Whitespace content

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Comment content cannot be empty.", result.Message);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Never);
        }

        // Test Case 6: Business Logic Failure - Post Not Found
        [Fact]
        [Trait("Category", "CreateComment - BusinessFailure")]
        public async Task Handle_PostNotFound_ReturnsNotFoundAndRollsBack()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, null, Guid.NewGuid(), "Content");

            _postRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                               .ReturnsAsync((Domain.Models.Post?)null); // Post not found

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Post not found or is not available for commenting.", result.Message);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Once); // Transaction started
            _unitOfWorkMock.Verify(uow => uow.RollbackAsync(), Times.Once);          // But rolled back
            _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
        }

        // Test Case 7: Business Logic Failure - Post Not Published
        [Fact]
        [Trait("Category", "CreateComment - BusinessFailure")]
        public async Task Handle_PostNotPublished_ReturnsNotFoundAndRollsBack()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, null, Guid.NewGuid(), "Content");
            _postRepositoryMock.Setup(r => r.GetOneAsync(It.Is<Expression<Func<Domain.Models.Post, bool>>>(
                 expr => expr.Compile()(new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false }) 
             ), null, null))
                               .ReturnsAsync((Domain.Models.Post?)null); 

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status); // Expect 404 because the specific query returns null
            Assert.Equal("Post not found or is not available for commenting.", result.Message);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
        }

        // Test Case 8: Business Logic Failure - Parent Comment Not Found
        [Fact]
        [Trait("Category", "CreateComment - BusinessFailure")]
        public async Task Handle_ParentCommentNotFound_ReturnsNotFoundAndRollsBack()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, parentCommentId, Guid.NewGuid(), "Reply content");
            var validPost = new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false };

            _postRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                               .ReturnsAsync(validPost);
            _commentRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null))
                               .ReturnsAsync((Domain.Models.Comment?)null); // Parent not found

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Parent comment not found.", result.Message);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
        }

        // Test Case 9: Business Logic Failure - Parent Comment on Different Post
        [Fact]
        [Trait("Category", "CreateComment - BusinessFailure")]
        public async Task Handle_ParentCommentOnDifferentPost_ReturnsBadRequestAndRollsBack()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var differentPostId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, parentCommentId, Guid.NewGuid(), "Reply content");
            var validPost = new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false };
            var parentOnDifferentPost = new Domain.Models.Comment { CommentId = parentCommentId, PostId = differentPostId, IsDeleted = false }; // Belongs to another post

            _postRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                              .ReturnsAsync(validPost);
            _commentRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null))
                              .ReturnsAsync(parentOnDifferentPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Reply must belong to the same post as the parent comment.", result.Message);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Never);
        }


        // Test Case 10: Exception Handling - Commit Fails
        [Fact]
        [Trait("Category", "CreateComment - Exception")]
        public async Task Handle_CommitThrowsException_ReturnsInternalErrorAndRollsBack()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, null, Guid.NewGuid(), "Content");
            var validPost = new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false };
            var exceptionMessage = "Database commit failed";

            _postRepositoryMock.Setup(r => r.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                               .ReturnsAsync(validPost);
            _commentRepositoryMock.Setup(r => r.AddAsync(It.IsAny<Domain.Models.Comment>())).Returns(Task.CompletedTask);
            _unitOfWorkMock.Setup(uow => uow.CommitAsync()).ThrowsAsync(new InvalidOperationException(exceptionMessage));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains(exceptionMessage, result.Message);
            Assert.Equal(Guid.Empty, result.ResponseData);
            _unitOfWorkMock.Verify(uow => uow.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(uow => uow.CommitAsync(), Times.Once); // Commit was attempted
            _unitOfWorkMock.Verify(uow => uow.RollbackAsync(), Times.Once); // Rollback was called due to exception
        }
    }
}
