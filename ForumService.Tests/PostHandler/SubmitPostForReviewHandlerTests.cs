using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Handler.Post.Command;
using ForumService.Core.Interfaces;
using ForumService.Domain.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostHandler
{
    public class SubmitPostForReviewHandlerTests
    {
        // Mock Dependencies
        private readonly Mock<IGenericRepository<ForumService.Domain.Models.Post>> _postRepositoryMock;
        private readonly Mock<IGenericRepository<Tag>> _tagRepositoryMock;
        private readonly Mock<IGenericRepository<PostTag>> _postTagRepositoryMock;
        private readonly Mock<IGenericRepository<BannedKeyword>> _bannedKeywordRepositoryMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;

        // Handler under test
        private readonly SubmitPostForReviewCommandHandler _handler;

        public SubmitPostForReviewHandlerTests()
        {
            _postRepositoryMock = new Mock<IGenericRepository<ForumService.Domain.Models.Post>>();
            _tagRepositoryMock = new Mock<IGenericRepository<Tag>>();
            _postTagRepositoryMock = new Mock<IGenericRepository<PostTag>>();
            _bannedKeywordRepositoryMock = new Mock<IGenericRepository<BannedKeyword>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();

            _handler = new SubmitPostForReviewCommandHandler(
                _postRepositoryMock.Object,
                _tagRepositoryMock.Object,
                _postTagRepositoryMock.Object,
                _bannedKeywordRepositoryMock.Object,
                _unitOfWorkMock.Object
            );
        }

        private void SetupNoBannedKeywords()
        {
            _bannedKeywordRepositoryMock.Setup(x => x.GetListAsync(
                    It.IsAny<Expression<Func<BannedKeyword, bool>>>(),                    
                    It.IsAny<Expression<Func<IQueryable<BannedKeyword>, IOrderedQueryable<BannedKeyword>>>>(), 
                    It.IsAny<Expression<Func<IQueryable<BannedKeyword>, IQueryable<BannedKeyword>>>>(),        
                    It.IsAny<int?>(), 
                    It.IsAny<int?>()  
                ))
                .ReturnsAsync(new List<BannedKeyword>());
        }

        // Test Case 1: Happy Path
        [Fact]
        [Trait("Category", "SubmitPostHandler - HappyPath")]
        public async Task Handle_WhenValidRequest_ShouldUpdateStatusAndCommit()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var tags = new List<string> { "csharp", "dotnet" };

            var existingPost = new ForumService.Domain.Models.Post
            {
                PostId = postId,
                AuthorId = userId,
                Status = "Draft",
                IsDeleted = false,
                Title = "Valid Title",
                Content = "Valid Content"
            };

            var command = new SubmitPostForReviewCommand(postId, userId, tags);

            _postRepositoryMock.Setup(x => x.GetByIdAsync(postId)).ReturnsAsync(existingPost);
            SetupNoBannedKeywords();

            _postTagRepositoryMock.Setup(x => x.GetListAsync(
                    It.IsAny<Expression<Func<PostTag, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<PostTag>, IOrderedQueryable<PostTag>>>>(),
                    It.IsAny<Expression<Func<IQueryable<PostTag>, IQueryable<PostTag>>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>()
                ))
                .ReturnsAsync(new List<PostTag>());

            _tagRepositoryMock.Setup(x => x.GetListAsync(
                    It.IsAny<Expression<Func<Tag, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<Tag>, IOrderedQueryable<Tag>>>>(),
                    It.IsAny<Expression<Func<IQueryable<Tag>, IQueryable<Tag>>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>()
                ))
                .ReturnsAsync(new List<Tag>());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("PendingReview", existingPost.Status);

            _unitOfWorkMock.Verify(x => x.BeginTransactionAsync(), Times.Once);
            _postRepositoryMock.Verify(x => x.UpdateAsync(existingPost), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        // Test Case 2: Post Not Found
        [Fact]
        [Trait("Category", "SubmitPostHandler - Validation")]
        public async Task Handle_WhenPostNotFound_Returns404()
        {
            // Arrange
            var command = new SubmitPostForReviewCommand(Guid.NewGuid(), Guid.NewGuid(), new List<string>());
            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((ForumService.Domain.Models.Post)null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.Equal("Post not found.", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 3: Post Deleted
        [Fact]
        [Trait("Category", "SubmitPostHandler - Validation")]
        public async Task Handle_WhenPostIsDeleted_Returns404()
        {
            // Arrange
            var existingPost = new ForumService.Domain.Models.Post { IsDeleted = true };
            var command = new SubmitPostForReviewCommand(Guid.NewGuid(), Guid.NewGuid(), new List<string>());

            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(404, result.Status);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 4: Unauthorized (Wrong User)
        [Fact]
        [Trait("Category", "SubmitPostHandler - Validation")]
        public async Task Handle_WhenUserIsNotAuthor_Returns403()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var requesterId = Guid.NewGuid();
            var existingPost = new ForumService.Domain.Models.Post
            {
                AuthorId = authorId,
                IsDeleted = false
            };

            var command = new SubmitPostForReviewCommand(Guid.NewGuid(), requesterId, new List<string>());
            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.Contains("not authorized", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 5: Invalid Status (Not Draft)
        [Fact]
        [Trait("Category", "SubmitPostHandler - Validation")]
        public async Task Handle_WhenPostStatusIsNotDraft_Returns400()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingPost = new ForumService.Domain.Models.Post
            {
                AuthorId = userId,
                IsDeleted = false,
                Status = "Published"
            };

            var command = new SubmitPostForReviewCommand(Guid.NewGuid(), userId, new List<string>());
            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(existingPost);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("not in Draft status", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 6: Banned Keyword in Title
        [Fact]
        [Trait("Category", "SubmitPostHandler - ContentModeration")]
        public async Task Handle_WhenTitleContainsBannedKeyword_Returns400()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingPost = new ForumService.Domain.Models.Post
            {
                AuthorId = userId,
                Status = "Draft",
                Title = "Hello badword world",
                Content = "Safe content"
            };

            var command = new SubmitPostForReviewCommand(Guid.NewGuid(), userId, new List<string>());
            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(existingPost);

            _bannedKeywordRepositoryMock.Setup(x => x.GetListAsync(
                    It.IsAny<Expression<Func<BannedKeyword, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<BannedKeyword>, IOrderedQueryable<BannedKeyword>>>>(),
                    It.IsAny<Expression<Func<IQueryable<BannedKeyword>, IQueryable<BannedKeyword>>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>()
                ))
                .ReturnsAsync(new List<BannedKeyword>
                {
                    new BannedKeyword { Keyword = "badword", IsActive = true }
                });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("Tiêu đề bài viết chứa từ khóa không phù hợp", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 7: Banned Keyword in Content
        [Fact]
        [Trait("Category", "SubmitPostHandler - ContentModeration")]
        public async Task Handle_WhenContentContainsBannedKeyword_Returns400()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingPost = new ForumService.Domain.Models.Post
            {
                AuthorId = userId,
                Status = "Draft",
                Title = "Safe Title",
                Content = "This contains spam content"
            };

            var command = new SubmitPostForReviewCommand(Guid.NewGuid(), userId, new List<string>());
            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(existingPost);

            _bannedKeywordRepositoryMock.Setup(x => x.GetListAsync(
                    It.IsAny<Expression<Func<BannedKeyword, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<BannedKeyword>, IOrderedQueryable<BannedKeyword>>>>(),
                    It.IsAny<Expression<Func<IQueryable<BannedKeyword>, IQueryable<BannedKeyword>>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>()
                ))
                .ReturnsAsync(new List<BannedKeyword>
                {
                    new BannedKeyword { Keyword = "spam", IsActive = true }
                });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Contains("Nội dung bài viết chứa từ khóa không phù hợp", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }

        // Test Case 8: Exception Handling
        [Fact]
        [Trait("Category", "SubmitPostHandler - Exception")]
        public async Task Handle_WhenExceptionOccursDuringCommit_Returns500()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingPost = new ForumService.Domain.Models.Post
            {
                AuthorId = userId,
                Status = "Draft",
                Title = "Valid",
                Content = "Valid"
            };
            var command = new SubmitPostForReviewCommand(Guid.NewGuid(), userId, new List<string>());

            _postRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(existingPost);
            SetupNoBannedKeywords();

            _postTagRepositoryMock.Setup(x => x.GetListAsync(
                    It.IsAny<Expression<Func<PostTag, bool>>>(),
                    It.IsAny<Expression<Func<IQueryable<PostTag>, IOrderedQueryable<PostTag>>>>(),
                    It.IsAny<Expression<Func<IQueryable<PostTag>, IQueryable<PostTag>>>>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>()
                ))
                .ReturnsAsync(new List<PostTag>());

            // Simulate Exception at Commit
            _unitOfWorkMock.Setup(x => x.CommitAsync()).ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Database error", result.Message);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }
    }
}
