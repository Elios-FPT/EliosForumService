using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Handler.Comment.Command;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Tests.Handlers.Comment
{
    public class UpdateCommentCommandHandlerTests
    {
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IGenericRepository<BannedKeyword>> _bannedKeywordRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly UpdateCommentCommandHandler _handler;

        public UpdateCommentCommandHandlerTests()
        {
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _bannedKeywordRepoMock = new Mock<IGenericRepository<BannedKeyword>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new UpdateCommentCommandHandler(
                _commentRepoMock.Object,
                _bannedKeywordRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenContentIsInvalid()
        {
            // Arrange
            var command = new UpdateCommentCommand(
                CommentId: Guid.NewGuid(),
                RequesterId: Guid.NewGuid(),
                Content: ""
            );

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("Content cannot be empty", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenContentContainsBannedKeyword()
        {
            // Arrange
            var command = new UpdateCommentCommand(
                CommentId: Guid.NewGuid(),
                RequesterId: Guid.NewGuid(),
                Content: "Nội dung chứa từ cấm"
            );

            var bannedList = new List<BannedKeyword> { new BannedKeyword { Keyword = "từ cấm", IsActive = true } };

            // Mock GetListAsync cho BannedKeyword
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(
                It.IsAny<Expression<Func<BannedKeyword, bool>>>(),
                null, null, null, null
            )).ReturnsAsync(bannedList);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Nội dung bình luận chứa từ khóa không phù hợp.", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenCommentNotFound()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var command = new UpdateCommentCommand(
                CommentId: commentId,
                RequesterId: Guid.NewGuid(),
                Content: "Valid content"
            );

            // Mock banned keyword rỗng
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<BannedKeyword>());

            // Mock comment không tồn tại
            _commentRepoMock.Setup(x => x.GetByIdAsync(commentId))
                .ReturnsAsync((Domain.Models.Comment)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Comment not found.", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenCommentIsDeleted()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var command = new UpdateCommentCommand(
                CommentId: commentId,
                RequesterId: Guid.NewGuid(),
                Content: "Valid content"
            );

            var comment = new Domain.Models.Comment { CommentId = commentId, IsDeleted = true };

            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<BannedKeyword>());

            _commentRepoMock.Setup(x => x.GetByIdAsync(commentId))
                .ReturnsAsync(comment);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Comment not found.", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn403_WhenUserIsNotAuthor()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var requesterId = Guid.NewGuid(); // Khác AuthorId

            // FIX: Dùng Named Arguments để đảm bảo gán đúng ID
            var command = new UpdateCommentCommand(
                CommentId: commentId,
                RequesterId: requesterId,
                Content: "New Content"
            );

            var comment = new Domain.Models.Comment
            {
                CommentId = commentId,
                AuthorId = authorId,
                IsDeleted = false
            };

            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<BannedKeyword>());

            _commentRepoMock.Setup(x => x.GetByIdAsync(commentId)).ReturnsAsync(comment);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.Equal("You are not authorized to edit this comment.", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldUpdateComment_WhenRequestIsValid()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var authorId = Guid.NewGuid();

            var command = new UpdateCommentCommand(
                CommentId: commentId,
                RequesterId: authorId, // Requester = Author
                Content: "Updated Content"
            );

            var comment = new Domain.Models.Comment
            {
                CommentId = commentId,
                AuthorId = authorId,
                Content = "Old Content",
                IsDeleted = false
            };

            _commentRepoMock.Setup(x => x.GetByIdAsync(commentId)).ReturnsAsync(comment);

            // Mock không có từ cấm
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(
                It.IsAny<Expression<Func<BannedKeyword, bool>>>(),
                null, null, null, null
            )).ReturnsAsync(new List<BannedKeyword>());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Comment updated successfully.", result.Message);
            Assert.Equal("Updated Content", comment.Content); // Verify content đã đổi

            _commentRepoMock.Verify(x => x.UpdateAsync(comment), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenExceptionOccurs()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var command = new UpdateCommentCommand(
                CommentId: commentId,
                RequesterId: Guid.NewGuid(),
                Content: "Content"
            );

            // Bypass check banned keywords
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<BannedKeyword>());

            _commentRepoMock.Setup(x => x.GetByIdAsync(commentId))
                .ThrowsAsync(new Exception("Database failed"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("An error occurred while updating the comment", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }
    }
}