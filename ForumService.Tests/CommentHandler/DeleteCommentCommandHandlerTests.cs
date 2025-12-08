using ForumService.Core.Handler.Comment.Command;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions; 
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Tests.CommentHandler
{
    public class DeleteCommentCommandHandlerTests
    {
        // Mocks
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        // System Under Test
        private readonly DeleteCommentCommandHandler _handler;

        public DeleteCommentCommandHandlerTests()
        {
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new DeleteCommentCommandHandler(
                _commentRepoMock.Object,
                _postRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        // Test Case 1: Comment Not Found
        // Scenario: CommentId does not exist in DB.
        // Expected: Returns 404, Rollback.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_CommentNotFound_ReturnsNotFound()
        {
            // Arrange
            var command = new DeleteCommentCommand(Guid.NewGuid(), Guid.NewGuid());

            _commentRepoMock.Setup(r => r.GetByIdAsync(command.CommentId))
                .ReturnsAsync((Domain.Models.Comment?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Comment not found.", result.Message);
            Assert.False(result.ResponseData);

            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 2: Comment Already Deleted
        // Scenario: Comment exists but IsDeleted is true.
        // Expected: Returns 404 (treated as not found), Rollback.
        [Fact]
        [Trait("Category", "Handler - Validation")]
        public async Task Handle_CommentAlreadyDeleted_ReturnsNotFound()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var command = new DeleteCommentCommand(commentId, Guid.NewGuid());
            var comment = new Domain.Models.Comment { CommentId = commentId, IsDeleted = true };

            _commentRepoMock.Setup(r => r.GetByIdAsync(commentId))
                .ReturnsAsync(comment);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 3: Parent Post Not Found
        // Scenario: Comment exists, but associated Post is missing.
        // Expected: Returns 404, Rollback.
        [Fact]
        [Trait("Category", "Handler - DataIntegrity")]
        public async Task Handle_ParentPostNotFound_ReturnsNotFound()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new DeleteCommentCommand(commentId, Guid.NewGuid());

            var comment = new Domain.Models.Comment
            {
                CommentId = commentId,
                PostId = postId,
                IsDeleted = false
            };

            _commentRepoMock.Setup(r => r.GetByIdAsync(commentId)).ReturnsAsync(comment);
            _postRepoMock.Setup(r => r.GetByIdAsync(postId)).ReturnsAsync((Domain.Models.Post?)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Parent post not found.", result.Message);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 4: Authorization Failure
        // Scenario: Requester is neither the Comment Author nor the Post Author.
        // Expected: Returns 403 Forbidden, Rollback.
        [Fact]
        [Trait("Category", "Handler - Authorization")]
        public async Task Handle_UnauthorizedUser_ReturnsForbidden()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var requesterId = Guid.NewGuid(); // Stranger
            var commentAuthorId = Guid.NewGuid();
            var postAuthorId = Guid.NewGuid();

            var command = new DeleteCommentCommand(commentId, requesterId);

            var comment = new Domain.Models.Comment
            {
                CommentId = commentId,
                PostId = postId,
                AuthorId = commentAuthorId,
                IsDeleted = false
            };

            var post = new Domain.Models.Post
            {
                PostId = postId,
                AuthorId = postAuthorId
            };

            _commentRepoMock.Setup(r => r.GetByIdAsync(commentId)).ReturnsAsync(comment);
            _postRepoMock.Setup(r => r.GetByIdAsync(postId)).ReturnsAsync(post);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.Equal("You are not authorized to delete this comment.", result.Message);
            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
        }

        // Test Case 5: Happy Path - Recursive Deletion
        // Scenario: Author deletes a comment which has 1 reply.
        // Expected: Both comments marked IsDeleted, Post.CommentCount reduced by 2, Transaction Committed.
        [Fact]
        [Trait("Category", "Handler - HappyPath")]
        public async Task Handle_ValidRequest_RecursivelyDeletesCommentsAndUpdatesPost()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var mainCommentId = Guid.NewGuid();
            var replyCommentId = Guid.NewGuid();
            var command = new DeleteCommentCommand(mainCommentId, requesterId);
            // Mock Post (Start with 10 comments)
            var post = new Domain.Models.Post
            {
                PostId = postId,
                AuthorId = Guid.NewGuid(), // Post author different
                CommentCount = 10
            };

            // Mock Main Comment
            var mainComment = new Domain.Models.Comment
            {
                CommentId = mainCommentId,
                PostId = postId,
                AuthorId = requesterId, // Requester is comment author
                IsDeleted = false
            };

            // Mock Reply Comment
            var replyComment = new Domain.Models.Comment
            {
                CommentId = replyCommentId,
                ParentCommentId = mainCommentId,
                PostId = postId,
                IsDeleted = false
            };

            _commentRepoMock.Setup(r => r.GetByIdAsync(mainCommentId)).ReturnsAsync(mainComment);
            _commentRepoMock.Setup(r => r.GetByIdAsync(replyCommentId)).ReturnsAsync(replyComment);
            _postRepoMock.Setup(r => r.GetByIdAsync(postId)).ReturnsAsync(post);

            _commentRepoMock.SetupSequence(r => r.GetListAsync(
                    It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(),
                    null, null, null, null))
                .ReturnsAsync(new List<Domain.Models.Comment> { replyComment }) // First call: find replies of main
                .ReturnsAsync(new List<Domain.Models.Comment>());               // Second call: find replies of reply

            // Setup UpdateAsync to capture changes
            _commentRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Domain.Models.Comment>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Contains("1 replies deleted", result.Message);
            Assert.True(mainComment.IsDeleted);
            Assert.NotNull(mainComment.DeletedAt);
            Assert.True(replyComment.IsDeleted);
            Assert.NotNull(replyComment.DeletedAt);
            Assert.Equal(8, post.CommentCount);
            Assert.Equal(requesterId, post.UpdatedBy);
            _postRepoMock.Verify(r => r.UpdateAsync(post), Times.Once);

            // Verify Transaction
            _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
        }

        // Test Case 6: Exception Handling
        // Scenario: Database error during update.
        // Expected: Returns 500, Rollback.
        [Fact]
        [Trait("Category", "Handler - Exception")]
        public async Task Handle_ExceptionOccurs_RollsBackTransaction()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var postId = Guid.NewGuid();

            var command = new DeleteCommentCommand(commentId, requesterId);

            var comment = new Domain.Models.Comment { CommentId = commentId, PostId = postId, AuthorId = requesterId };
            var post = new Domain.Models.Post { PostId = postId, CommentCount = 5 };

            _commentRepoMock.Setup(r => r.GetByIdAsync(commentId)).ReturnsAsync(comment);
            _postRepoMock.Setup(r => r.GetByIdAsync(postId)).ReturnsAsync(post);
            _commentRepoMock.Setup(r => r.GetListAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<Domain.Models.Comment>()); // No replies

            // Simulate DB Error on Update
            _commentRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Domain.Models.Comment>()))
                .ThrowsAsync(new Exception("DB Connection Timeout"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.StartsWith("An error occurred while deleting the comment", result.Message);

            _unitOfWorkMock.Verify(u => u.RollbackAsync(), Times.Once);
            _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Never);
        }
    }
}
