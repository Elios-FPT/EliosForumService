using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects;
using ForumService.Core.Handler.Comment.Command;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Tests.Handlers.Comment
{
    public class CreateCommentCommandHandlerTests
    {
        private readonly Mock<IGenericRepository<Domain.Models.Comment>> _commentRepoMock;
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<BannedKeyword>> _bannedKeywordRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ISUtilityServiceClient> _utilityServiceMock;
        private readonly Mock<ILogger<CreateCommentCommandHandler>> _loggerMock;
        private readonly CreateCommentCommandHandler _handler;

        public CreateCommentCommandHandlerTests()
        {
            _commentRepoMock = new Mock<IGenericRepository<Domain.Models.Comment>>();
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _bannedKeywordRepoMock = new Mock<IGenericRepository<BannedKeyword>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _utilityServiceMock = new Mock<ISUtilityServiceClient>();
            _loggerMock = new Mock<ILogger<CreateCommentCommandHandler>>();

            // FIX: Đảo vị trí tham số 3 và 4 cho khớp với Handler mới
            _handler = new CreateCommentCommandHandler(
                _commentRepoMock.Object,
                _postRepoMock.Object,
                _unitOfWorkMock.Object,        
                _bannedKeywordRepoMock.Object, 
                _utilityServiceMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenInputIsInvalid()
        {
            // PostId, ParentId, AuthorId, Content
            var command = new CreateCommentCommand(Guid.NewGuid(), null, Guid.NewGuid(), "");

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(400, result.Status);
            Assert.Contains("content cannot be empty", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenContentContainsBannedKeyword()
        {
            var command = new CreateCommentCommand(Guid.NewGuid(), null, Guid.NewGuid(), "Nội dung chứa từ cấm");
            var bannedList = new List<BannedKeyword> { new BannedKeyword { Keyword = "từ cấm", IsActive = true } };

            // Mock GetListAsync: trả về danh sách từ cấm
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(
                It.IsAny<Expression<Func<BannedKeyword, bool>>>(),
                null, null, null, null
            )).ReturnsAsync(bannedList);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(400, result.Status);
            Assert.Contains("từ khóa không phù hợp", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenPostNotFound()
        {
            var postId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, null, Guid.NewGuid(), "Valid content");

            // Mock Post trả về null
            _postRepoMock.Setup(x => x.GetOneAsync(
                It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(),
                null, null
            )).ReturnsAsync((Domain.Models.Post)null);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(404, result.Status);
            Assert.Contains("Post not found", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenParentCommentNotFound()
        {
            var postId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, parentCommentId, Guid.NewGuid(), "Reply content");

            var post = new Domain.Models.Post { PostId = postId, Status = "Published", IsDeleted = false };

            _postRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(post);

            // Mock Parent Comment trả về null
            _commentRepoMock.Setup(x => x.GetOneAsync(
                It.Is<Expression<Func<Domain.Models.Comment, bool>>>(exp => exp.Compile().Invoke(new Domain.Models.Comment { CommentId = parentCommentId, IsDeleted = false })),
                null, null
            )).ReturnsAsync((Domain.Models.Comment)null);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(404, result.Status);
            Assert.Equal("Parent comment not found.", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenParentCommentBelongsToDifferentPost()
        {
            var postId = Guid.NewGuid();
            var otherPostId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, parentCommentId, Guid.NewGuid(), "Reply content");

            var post = new Domain.Models.Post { PostId = postId, Status = "Published" };
            // Parent comment có PostId khác với Post hiện tại
            var parentComment = new Domain.Models.Comment { CommentId = parentCommentId, PostId = otherPostId };

            _postRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(post);

            _commentRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null))
                .ReturnsAsync(parentComment);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(400, result.Status);
            Assert.Contains("must belong to the same post", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldCreateComment_AndSendNotification_WhenRequestIsValid()
        {
            var authorId = Guid.NewGuid();
            var postAuthorId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new CreateCommentCommand(postId, null, authorId, "Nice post!");

            var post = new Domain.Models.Post
            {
                PostId = postId,
                AuthorId = postAuthorId,
                Status = "Published",
                CommentCount = 10
            };

            _postRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(post);

            // Mock tạo comment thành công
            _commentRepoMock.Setup(x => x.AddAsync(It.IsAny<Domain.Models.Comment>()))
                .Returns(Task.CompletedTask);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(201, result.Status);

            // Verify
            _commentRepoMock.Verify(x => x.AddAsync(It.Is<Domain.Models.Comment>(c =>
                c.PostId == postId &&
                c.Content == "Nice post!" &&
                c.AuthorId == authorId
            )), Times.Once);

            Assert.Equal(11, post.CommentCount);
            _postRepoMock.Verify(x => x.UpdateAsync(post), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);

            _utilityServiceMock.Verify(x => x.SendNotificationAsync(It.Is<NotificationDto>(n =>
                n.UserId == postAuthorId &&
                n.Title == "Someone commented on your post"
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCreateReply_AndNotifyParentCommentAuthor()
        {
            var replyAuthorId = Guid.NewGuid();
            var parentCommentAuthorId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var parentCommentId = Guid.NewGuid();

            var command = new CreateCommentCommand(postId, parentCommentId, replyAuthorId, "I agree!");

            var post = new Domain.Models.Post { PostId = postId, Status = "Published" };
            var parentComment = new Domain.Models.Comment { CommentId = parentCommentId, PostId = postId, AuthorId = parentCommentAuthorId };

            _postRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ReturnsAsync(post);

            _commentRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Comment, bool>>>(), null, null))
                .ReturnsAsync(parentComment);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(201, result.Status);

            _utilityServiceMock.Verify(x => x.SendNotificationAsync(It.Is<NotificationDto>(n =>
                n.UserId == parentCommentAuthorId &&
                n.Title == "Someone replied to your comment"
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenExceptionOccurs()
        {
            var command = new CreateCommentCommand(Guid.NewGuid(), null, Guid.NewGuid(), "Content");

            _postRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Domain.Models.Post, bool>>>(), null, null))
                .ThrowsAsync(new Exception("DB Connection Failed"));

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(500, result.Status);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }
    }
}