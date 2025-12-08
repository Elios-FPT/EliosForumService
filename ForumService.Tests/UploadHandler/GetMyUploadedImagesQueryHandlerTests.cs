using ForumService.Contract.TransferObjects;
using ForumService.Core.Handler.Upload;
using ForumService.Core.Interfaces;
using Moq;
using System.Linq.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Upload.Request;

namespace ForumService.Tests.UploadHandler
{
    public class GetMyUploadedImagesQueryHandlerTests
    {
        private readonly Mock<IGenericRepository<Domain.Models.Attachment>> _mockAttachmentRepo;
        private readonly GetMyUploadedImagesQueryHandler _handler;

        public GetMyUploadedImagesQueryHandlerTests()
        {
            _mockAttachmentRepo = new Mock<IGenericRepository<Domain.Models.Attachment>>();
            _handler = new GetMyUploadedImagesQueryHandler(_mockAttachmentRepo.Object);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenUserIdIsEmpty()
        {
            // Arrange
            var query = new GetMyUploadedImagesQuery(Guid.Empty);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("User ID is required.", result.Message);

            _mockAttachmentRepo.Verify(x => x.GetListAsyncUntracked(
                It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(),              // 1. filter
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IOrderedQueryable<Domain.Models.Attachment>>>>(), // 2. orderBy
                It.IsAny<Expression<Func<Domain.Models.Attachment, UploadFileResponseDto>>>(), // 3. selector
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IQueryable<Domain.Models.Attachment>>>>(), // 4. include
                It.IsAny<int?>(), // 5. pageSize
                It.IsAny<int?>()  // 6. pageNumber
            ), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturn200AndList_WhenImagesFound()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new GetMyUploadedImagesQuery(userId);

            var expectedImages = new List<UploadFileResponseDto>
            {
                new UploadFileResponseDto { AttachmentId = Guid.NewGuid(), FileName = "img1.jpg", Url = "http://url1", ContentType = "image/jpeg" },
                new UploadFileResponseDto { AttachmentId = Guid.NewGuid(), FileName = "img2.png", Url = "http://url2", ContentType = "image/png" }
            };

            // Setup Mock
            _mockAttachmentRepo.Setup(x => x.GetListAsyncUntracked(
                It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(),              // 1. filter
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IOrderedQueryable<Domain.Models.Attachment>>>>(), // 2. orderBy
                It.IsAny<Expression<Func<Domain.Models.Attachment, UploadFileResponseDto>>>(), // 3. selector
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IQueryable<Domain.Models.Attachment>>>>(), // 4. include
                It.IsAny<int?>(), // 5. pageSize
                It.IsAny<int?>()  // 6. pageNumber
            )).ReturnsAsync(expectedImages);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(2, result.ResponseData.Count);
            Assert.Equal("img1.jpg", result.ResponseData[0].FileName);
        }

        [Fact]
        public async Task Handle_ShouldReturn200AndEmptyList_WhenNoImagesFound()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var query = new GetMyUploadedImagesQuery(userId);

            _mockAttachmentRepo.Setup(x => x.GetListAsyncUntracked(
                It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(),
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IOrderedQueryable<Domain.Models.Attachment>>>>(),
                It.IsAny<Expression<Func<Domain.Models.Attachment, UploadFileResponseDto>>>(),
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IQueryable<Domain.Models.Attachment>>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()
            )).ReturnsAsync(new List<UploadFileResponseDto>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Empty(result.ResponseData);
        }

        [Fact]
        public async Task Handle_ShouldReturn500_WhenRepositoryThrowsException()
        {
            // Arrange
            var query = new GetMyUploadedImagesQuery(Guid.NewGuid());

            // Setup Mock ném ra Exception
            _mockAttachmentRepo.Setup(x => x.GetListAsyncUntracked(
                It.IsAny<Expression<Func<Domain.Models.Attachment, bool>>>(),
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IOrderedQueryable<Domain.Models.Attachment>>>>(),
                It.IsAny<Expression<Func<Domain.Models.Attachment, UploadFileResponseDto>>>(),
                It.IsAny<Expression<Func<IQueryable<Domain.Models.Attachment>, IQueryable<Domain.Models.Attachment>>>>(),
                It.IsAny<int?>(),
                It.IsAny<int?>()
            )).ThrowsAsync(new Exception("Database connection failed"));

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Database connection failed", result.Message);
        }
    }
}
