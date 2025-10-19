using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Post.Command;
using static ForumService.Contract.UseCases.Post.Request;

namespace ForumService.Tests.PostController
{
    public class CreatePostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly ForumService.Web.Controllers.Post.PostController _controller;

        public CreatePostTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new ForumService.Web.Controllers.Post.PostController(_senderMock.Object);
        }

        // Helper method to create a mock IFormFile
        private IFormFile CreateMockFormFile(string fileName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            return new FormFile(
                baseStream: new MemoryStream(bytes),
                baseStreamOffset: 0,
                length: bytes.Length,
                name: "files", // Tên tham số trong controller
                fileName: fileName
            )
            {
                Headers = new HeaderDictionary(),
                ContentType = "text/plain"
            };
        }

        [Fact]
        [Trait("Category", "HappyPath")]
        public async Task CreatePost_WithValidDataAndNoFiles_ReturnsSuccess()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Valid Title", "Summary", "Valid Content");
            var files = new List<IFormFile>();
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request, files);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c => c.Title == request.Title && c.FilesToUpload.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "HappyPath")]
        public async Task CreatePost_WithValidDataAndOneFile_ReturnsSuccess()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Post With File", "Summary", "Content");
            var mockFile = CreateMockFormFile("test.txt", "file content");
            var files = new List<IFormFile> { mockFile };
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request, files);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c => c.FilesToUpload.Count == 1 && c.FilesToUpload[0].FileName == "test.txt"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "HappyPath")]
        public async Task CreatePost_WithValidDataAndMultipleFiles_ReturnsSuccess()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Post With Multiple Files", "Summary", "Content");
            var files = new List<IFormFile>
            {
                CreateMockFormFile("file1.txt", "content1"),
                CreateMockFormFile("file2.log", "content2")
            };
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request, files);

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c => c.FilesToUpload.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "EdgeCase")]
        public async Task CreatePost_WithZeroLengthFile_ShouldIgnoreTheFile()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Post With Empty File", "Summary", "Content");
            var emptyFile = new FormFile(new MemoryStream(), 0, 0, "files", "empty.txt");
            var files = new List<IFormFile> { emptyFile };
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request, files);

            // Assert
            Assert.Equal(200, result.Status);
            // Xác minh rằng command được gửi đi không chứa file nào
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c => c.FilesToUpload.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "EdgeCase")]
        public async Task CreatePost_WithNullFileList_ShouldSucceedWithoutFiles()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Post With Null Files", "Summary", "Content");
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request, null); // Truyền null vào list file

            // Assert
            Assert.Equal(200, result.Status);
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c => c.FilesToUpload.Count == 0), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "Failure")]
        public async Task CreatePost_HandlerReturnsBadRequest_ControllerReturnsSameResponse()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Invalid Post", "Summary", "Content");
            var files = new List<IFormFile>();
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Handler validation failed.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request, files);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Handler validation failed.", result.Message);
        }

        [Fact]
        [Trait("Category", "Failure")]
        public async Task CreatePost_HandlerReturnsInternalError_ControllerReturnsSameResponse()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Post that fails", "Summary", "Content");
            var mockFile = CreateMockFormFile("fail.txt", "content");
            var files = new List<IFormFile> { mockFile };
            var expectedResponse = new BaseResponseDto<bool> { Status = 500, Message = "Failed to upload file: fail.txt. Post creation cancelled.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request, files);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("Failed to upload file", result.Message);
        }

        [Fact]
        [Trait("Category", "Exception")]
        public async Task CreatePost_SenderThrowsException_ShouldPropagateException()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Exception Post", "Summary", "Content");
            var files = new List<IFormFile>();
            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new System.InvalidOperationException("Database connection failed"));

            // Act & Assert
            // Xác minh rằng exception được ném ra khỏi controller
            // Middleware xử lý exception của ASP.NET Core sẽ bắt nó và trả về response 500
            await Assert.ThrowsAsync<System.InvalidOperationException>(() => _controller.CreatePost(request, files));
        }

        [Fact]
        [Trait("Category", "Mapping")]
        public async Task CreatePost_ShouldMapAllRequestAndFileDataToCommandCorrectly()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var request = new CreatePostRequest(authorId, categoryId, "Mapping Test", "Summary text", "Content text", "Solution");
            var mockFile = CreateMockFormFile("mapping.txt", "map content");
            var files = new List<IFormFile> { mockFile };

            CreatePostCommand capturedCommand = null;
            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as CreatePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.CreatePost(request, files);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(request.AuthorId, capturedCommand.AuthorId);
            Assert.Equal(request.CategoryId, capturedCommand.CategoryId);
            Assert.Equal(request.Title, capturedCommand.Title);
            Assert.Equal(request.Summary, capturedCommand.Summary);
            Assert.Equal(request.Content, capturedCommand.Content);
            Assert.Equal(request.PostType, capturedCommand.PostType);
            Assert.Single(capturedCommand.FilesToUpload);
            Assert.Equal("mapping.txt", capturedCommand.FilesToUpload[0].FileName);
            Assert.Equal("text/plain", capturedCommand.FilesToUpload[0].ContentType);
            Assert.Equal(Encoding.UTF8.GetBytes("map content"), capturedCommand.FilesToUpload[0].Content);
        }

        [Fact]
        [Trait("Category", "Mapping")]
        public async Task CreatePost_WithNullSummaryAndCategory_ShouldMapCorrectly()
        {
            // Arrange
            var request = new CreatePostRequest(Guid.NewGuid(), null, "Title", null, "Content");
            var files = new List<IFormFile>();

            CreatePostCommand capturedCommand = null;
            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as CreatePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.CreatePost(request, files);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Null(capturedCommand.CategoryId);
            Assert.Null(capturedCommand.Summary);
            Assert.Equal(request.Title, capturedCommand.Title);
        }
    }
}
