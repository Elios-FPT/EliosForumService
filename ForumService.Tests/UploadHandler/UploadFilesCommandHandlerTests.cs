using ForumService.Contract.TransferObjects;
using ForumService.Core.Handler.Upload.Command;
using ForumService.Core.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Upload.Command;

namespace ForumService.Tests.UploadHandler
{
    public class UploadFilesCommandHandlerTests
    {
        // Mock declarations for dependencies
        private readonly Mock<IGenericRepository<Domain.Models.Attachment>> _mockAttachmentRepo;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ISUtilityServiceClient> _mockUtilityService;

        // Class under test
        private readonly UploadFilesCommandHandler _handler;

        public UploadFilesCommandHandlerTests()
        {
            // Initialize Mocks
            _mockAttachmentRepo = new Mock<IGenericRepository<Domain.Models.Attachment>>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUtilityService = new Mock<ISUtilityServiceClient>();

            // Inject Mocks into the Handler
            _handler = new UploadFilesCommandHandler(
                _mockAttachmentRepo.Object,
                _mockUnitOfWork.Object,
                _mockUtilityService.Object
            );
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenFilesListIsNull()
        {
            // Arrange
            var command = new UploadFilesCommand(Guid.NewGuid(), null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.Equal("No files to upload.", result.Message);

            // Verify that no transaction was started
            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturn400_WhenFilesListIsEmpty()
        {
            // Arrange
            var command = new UploadFilesCommand(Guid.NewGuid(), new List<FileToUploadDto>());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(400, result.Status);
            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Never);
        }

        [Fact]
        public async Task Handle_ShouldReturn200_WhenUploadAndSaveSuccess()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var fileDto = new FileToUploadDto
            {
                FileName = "test.jpg",
                ContentType = "image/jpeg",
                Content = new byte[10] // Dummy content
            };

            var command = new UploadFilesCommand(userId, new List<FileToUploadDto> { fileDto });
            var fakeUrl = "https://cloud-storage.com/test.jpg";

            // Setup Mock: Simulate successful upload returning a URL
            _mockUtilityService
                .Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<FileToUploadDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeUrl);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.Single(result.ResponseData); // Ensure one file is returned
            Assert.Equal(fakeUrl, result.ResponseData[0].Url);

            // Verify execution flow:
            // 1. Transaction started
            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            // 2. Upload Service called
            _mockUtilityService.Verify(s => s.UploadFileAsync(It.IsAny<string>(), fileDto, It.IsAny<CancellationToken>()), Times.Once);
            // 3. Repository AddRange called
            _mockAttachmentRepo.Verify(r => r.AddRangeAsync(It.IsAny<List<Domain.Models.Attachment>>()), Times.Once);
            // 4. Transaction committed
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ShouldRollbackAndReturn500_WhenUploadServiceFails()
        {
            // Arrange
            var fileDto = new FileToUploadDto { FileName = "fail.jpg", Content = new byte[10] };
            var command = new UploadFilesCommand(Guid.NewGuid(), new List<FileToUploadDto> { fileDto });

            // Setup Mock: Simulate upload failure (returns null or empty string)
            _mockUtilityService
                .Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<FileToUploadDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(string.Empty);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Failed to upload file", result.Message);

            // Verify: Must call Rollback
            _mockUnitOfWork.Verify(u => u.RollbackAsync(), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Never); // Should not commit
        }

        [Fact]
        public async Task Handle_ShouldRollbackAndReturn500_WhenDatabaseSaveFails()
        {
            // Arrange
            var fileDto = new FileToUploadDto { FileName = "db_fail.jpg", Content = new byte[10] };
            var command = new UploadFilesCommand(Guid.NewGuid(), new List<FileToUploadDto> { fileDto });

            // Setup Mock: Upload returns valid URL
            _mockUtilityService
                .Setup(s => s.UploadFileAsync(It.IsAny<string>(), It.IsAny<FileToUploadDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://valid-url.com");

            // Setup Mock: Simulate DB Commit failure
            _mockUnitOfWork
                .Setup(u => u.CommitAsync())
                .ThrowsAsync(new Exception("DB Connection Lost"));

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.Contains("Failed to upload files", result.Message);

            // Verify: Must call Rollback on DB error
            _mockUnitOfWork.Verify(u => u.RollbackAsync(), Times.Once);
        }
    }
}
