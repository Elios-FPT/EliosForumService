using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Handler.Post.Command;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostHandler
{
    public class UpdatePostCommandHandlerTests
    {
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<BannedKeyword>> _bannedKeywordRepoMock;
        private readonly Mock<IGenericRepository<Tag>> _tagRepoMock;
        private readonly Mock<IGenericRepository<PostTag>> _postTagRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly UpdatePostCommandHandler _handler;

        public UpdatePostCommandHandlerTests()
        {
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _bannedKeywordRepoMock = new Mock<IGenericRepository<BannedKeyword>>();
            _tagRepoMock = new Mock<IGenericRepository<Tag>>();
            _postTagRepoMock = new Mock<IGenericRepository<PostTag>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new UpdatePostCommandHandler(
                _postRepoMock.Object,
                _bannedKeywordRepoMock.Object,
                _tagRepoMock.Object,
                _postTagRepoMock.Object,
                _unitOfWorkMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenInputIsInvalid()
        {
            // Arrange
            var command = new UpdatePostCommand(Guid.NewGuid(), Guid.NewGuid(), "", "", null, null, null, false);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("cannot be empty", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn404_WhenPostNotFound()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var command = new UpdatePostCommand(Guid.NewGuid(), postId, "Valid Title", "Valid Content", null, null, null, false);

            _postRepoMock.Setup(x => x.GetByIdAsync(postId))
                .ReturnsAsync((Domain.Models.Post)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Contains("not found", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn403_WhenUserIsNotAuthor()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var authorId = Guid.NewGuid();
            var requesterId = Guid.NewGuid(); 

            var command = new UpdatePostCommand(requesterId, postId, "Title", "Content", null, null, null, false);
            var existingPost = new Domain.Models.Post { PostId = postId, AuthorId = authorId };

            _postRepoMock.Setup(x => x.GetByIdAsync(postId)).ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.Equal("You are not authorized to update this post.", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenTitleContainsBannedKeyword_AndCheckIsEnabled()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new UpdatePostCommand(userId, postId, "Chứa từ cấm chết tiệt", "Content", null, null, null, true);
            var existingPost = new Domain.Models.Post { PostId = postId, AuthorId = userId, PostType = "Post" };
            var bannedList = new List<BannedKeyword> { new BannedKeyword { Keyword = "chết tiệt", IsActive = true } };

            _postRepoMock.Setup(x => x.GetByIdAsync(postId)).ReturnsAsync(existingPost);
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(bannedList);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("Tiêu đề bài viết chứa từ khóa không phù hợp.", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldUpdateToDraft_AndSkipKeywordCheck_WhenPostTypeIsPost_AndNotSubmitted()
        {
            // Arrange: 
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new UpdatePostCommand(userId, postId, "Draft Title", "Chứa từ cấm nhưng đang lưu nháp", null, null, null, false);

            var existingPost = new Domain.Models.Post { PostId = postId, AuthorId = userId, PostType = "Post", Status = "Published" };
            var bannedList = new List<BannedKeyword> { new BannedKeyword { Keyword = "từ cấm", IsActive = true } };

            _postRepoMock.Setup(x => x.GetByIdAsync(postId)).ReturnsAsync(existingPost);
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(bannedList);
            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Draft", existingPost.Status);
            _postRepoMock.Verify(x => x.UpdateAsync(existingPost), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldUpdateToPendingReview_WhenPostTypeIsPost_AndSubmitted()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new UpdatePostCommand(userId, postId, "Review Title", "Clean Content", null, null, null, true);

            var existingPost = new Domain.Models.Post { PostId = postId, AuthorId = userId, PostType = "Post", Status = "Draft" };

            _postRepoMock.Setup(x => x.GetByIdAsync(postId)).ReturnsAsync(existingPost);
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<BannedKeyword>());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("PendingReview", existingPost.Status);
        }

        [Fact]
        public async Task Handle_ShouldUpdateToPublished_WhenPostTypeIsSolution()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new UpdatePostCommand(userId, postId, "Solution Update", "Clean Content", null, null, null, false); // false hay true đều Published

            var existingPost = new Domain.Models.Post { PostId = postId, AuthorId = userId, PostType = "Solution", Status = "Draft" };

            _postRepoMock.Setup(x => x.GetByIdAsync(postId)).ReturnsAsync(existingPost);
            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<BannedKeyword, bool>>>(), null, null, null, null))
                .ReturnsAsync(new List<BannedKeyword>());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Equal("Published", existingPost.Status);
        }

        [Fact]
        public async Task Handle_ShouldUpdateTags_Correctly()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var newTags = new List<string> { "NewTag" };
            var command = new UpdatePostCommand(userId, postId, "Title", "Content", null, newTags, null, false);

            var existingPost = new Domain.Models.Post { PostId = postId, AuthorId = userId, PostType = "Post" };
            var oldPostTags = new List<PostTag> { new PostTag { PostId = postId, TagId = Guid.NewGuid() } };

            _postRepoMock.Setup(x => x.GetByIdAsync(postId)).ReturnsAsync(existingPost);
            _postTagRepoMock.Setup(x => x.GetListAsync(It.IsAny<Expression<Func<PostTag, bool>>>(), null, null, null, null))
                .ReturnsAsync(oldPostTags);
            _tagRepoMock.Setup(x => x.GetOneAsync(It.IsAny<Expression<Func<Tag, bool>>>(), null, null))
                .ReturnsAsync((Tag)null);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            _postTagRepoMock.Verify(x => x.DeleteRangeAsync(oldPostTags), Times.Once);
            _tagRepoMock.Verify(x => x.AddAsync(It.Is<Tag>(t => t.Name == "newtag")), Times.Once);
            _postTagRepoMock.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<PostTag>>(pts => pts.Count() == 1)), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenExceptionOccurs()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var postId = Guid.NewGuid();
            var command = new UpdatePostCommand(userId, postId, "Title", "Content", null, null, null, false);

            _postRepoMock.Setup(x => x.GetByIdAsync(postId)).ThrowsAsync(new Exception("DB Error"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }
    }
}