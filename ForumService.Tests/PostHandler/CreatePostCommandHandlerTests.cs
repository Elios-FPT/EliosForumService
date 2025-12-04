using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Core.Handler.Post.Command;
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
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostHandler
{
    public class CreatePostCommandHandlerTests
    {
        private readonly Mock<IGenericRepository<Domain.Models.Post>> _postRepoMock;
        private readonly Mock<IGenericRepository<Tag>> _tagRepoMock;
        private readonly Mock<IGenericRepository<PostTag>> _postTagRepoMock;
        private readonly Mock<IGenericRepository<BannedKeyword>> _bannedKeywordRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IKafkaProducer> _kafkaProducerMock;
        private readonly Mock<IAppConfiguration> _appConfigMock;
        private readonly Mock<ILogger<CreatePostCommandHandler>> _loggerMock;
        private readonly CreatePostCommandHandler _handler;

        public CreatePostCommandHandlerTests()
        {
            _postRepoMock = new Mock<IGenericRepository<Domain.Models.Post>>();
            _tagRepoMock = new Mock<IGenericRepository<Tag>>();
            _postTagRepoMock = new Mock<IGenericRepository<PostTag>>();
            _bannedKeywordRepoMock = new Mock<IGenericRepository<BannedKeyword>>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _kafkaProducerMock = new Mock<IKafkaProducer>();
            _appConfigMock = new Mock<IAppConfiguration>();
            _loggerMock = new Mock<ILogger<CreatePostCommandHandler>>();

            _handler = new CreatePostCommandHandler(
                _postRepoMock.Object,
                _tagRepoMock.Object,
                _postTagRepoMock.Object,
                _bannedKeywordRepoMock.Object,
                _unitOfWorkMock.Object,
                _kafkaProducerMock.Object,
                _appConfigMock.Object,     
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenInputIsInvalid()
        {
            var command = new CreatePostCommand(Guid.Empty, null, "", "", null, null, null, false);
            var result = await _handler.Handle(command, CancellationToken.None);
            Assert.Equal(400, result.Status);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenTitleContainsBannedKeyword()
        {
            var command = new CreatePostCommand(Guid.NewGuid(), null, "Chứa từ cấm chết tiệt", "Nội dung sạch", "Post", null, null, true);

            var bannedList = new List<BannedKeyword> { new BannedKeyword { Keyword = "chết tiệt", IsActive = true } };

            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(
                It.IsAny<Expression<Func<BannedKeyword, bool>>>(),
                null, null, null, null
            )).ReturnsAsync(bannedList);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(400, result.Status);
            Assert.Equal("The post title contains a banned keyword: 'chết tiệt'", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenContentContainsBannedKeyword()
        {
            var command = new CreatePostCommand(Guid.NewGuid(), null, "Tiêu đề sạch", "Nội dung chứa đồ ngu", "Post", null, null, true);
            var bannedList = new List<BannedKeyword> { new BannedKeyword { Keyword = "đồ ngu", IsActive = true } };

            _bannedKeywordRepoMock.Setup(x => x.GetListAsync(
                It.IsAny<Expression<Func<BannedKeyword, bool>>>(),
                null, null, null, null
            )).ReturnsAsync(bannedList);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(400, result.Status);
            Assert.Equal("The post content contains a banned keyword: 'đồ ngu'", result.Message);
        }

        [Fact]
        public async Task Handle_ShouldCreateDraft_WhenPostTypeIsPostAndNotSubmitted()
        {
            var command = new CreatePostCommand(Guid.NewGuid(), null, "Draft Title", "Draft Content", "Post", null, null, false);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(200, result.Status);
            _postRepoMock.Verify(x => x.AddAsync(It.Is<Domain.Models.Post>(p => p.Status == "Draft")), Times.Once);
            _unitOfWorkMock.Verify(x => x.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCreatePendingReview_WhenPostTypeIsPostAndSubmitted()
        {
            var command = new CreatePostCommand(Guid.NewGuid(), null, "Review Title", "Review Content", "Post", null, null, true);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(200, result.Status);
            _postRepoMock.Verify(x => x.AddAsync(It.Is<Domain.Models.Post>(p => p.Status == "PendingReview")), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldCreatePublished_WhenPostTypeIsSolution()
        {
            var command = new CreatePostCommand(Guid.NewGuid(), null, "Solution Title", "Solution Content", "Solution", null, null, true);

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(200, result.Status);
            _postRepoMock.Verify(x => x.AddAsync(It.Is<Domain.Models.Post>(p => p.Status == "Published")), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldProcessTags_Correctly()
        {
            var tags = new List<string> { "NewTag", "ExistingTag" };
            var command = new CreatePostCommand(Guid.NewGuid(), null, "Tag Title", "Content", "Post", null, tags, false);

            _tagRepoMock.Setup(x => x.GetOneAsync(
                It.Is<Expression<Func<Tag, bool>>>(exp => exp.Compile().Invoke(new Tag { Name = "ExistingTag" })),
                null, null
            )).ReturnsAsync(new Tag { TagId = Guid.NewGuid(), Name = "ExistingTag" });

            _tagRepoMock.Setup(x => x.GetOneAsync(
                It.Is<Expression<Func<Tag, bool>>>(exp => exp.Compile().Invoke(new Tag { Name = "NewTag" })),
                null, null
            )).ReturnsAsync((Tag)null);

            await _handler.Handle(command, CancellationToken.None);

            _tagRepoMock.Verify(x => x.AddAsync(It.Is<Tag>(t => t.Name == "newtag")), Times.Once);
            _postTagRepoMock.Verify(x => x.AddRangeAsync(It.Is<IEnumerable<PostTag>>(pts => pts.Count() == 2)), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenExceptionOccurs()
        {
            var command = new CreatePostCommand(Guid.NewGuid(), null, "Error Title", "Content", "Post", null, null, false);

            _postRepoMock.Setup(x => x.AddAsync(It.IsAny<Domain.Models.Post>()))
                .ThrowsAsync(new Exception("Database error"));

            var result = await _handler.Handle(command, CancellationToken.None);

            Assert.Equal(500, result.Status);
            _unitOfWorkMock.Verify(x => x.RollbackAsync(), Times.Once);
        }
    }
}