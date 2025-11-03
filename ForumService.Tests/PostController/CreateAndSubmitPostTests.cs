using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;
using static ForumService.Contract.UseCases.Post.Request;
using ForumService.Contract.TransferObjects.Post; 

namespace ForumService.Tests.PostController
{
    public class CreateAndSubmitPostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public CreateAndSubmitPostTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.PostController(_senderMock.Object);

            // Mock HttpContext and Headers
            _httpContextMock = new Mock<HttpContext>();
            _httpRequestMock = new Mock<HttpRequest>();
            _headers = new HeaderDictionary();
            _httpRequestMock.Setup(x => x.Headers).Returns(_headers);
            _httpContextMock.Setup(x => x.Request).Returns(_httpRequestMock.Object);

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = _httpContextMock.Object,
            };
        }

        // Helper to set the auth header
        private void SetAuthHeader(string? value)
        {
            if (value != null)
            {
                _headers["X-Auth-Request-User"] = new StringValues(value);
            }
            else
            {
                _headers.Remove("X-Auth-Request-User");
            }
        }

        // Helper to mock a List<IFormFile>
        private (List<IFormFile> files, byte[] fileContent) MockFiles()
        {
            var fileMock = new Mock<IFormFile>();
            var content = "dummy file content";
            var fileContentBytes = Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(fileContentBytes);

            fileMock.Setup(f => f.FileName).Returns("test.txt");
            fileMock.Setup(f => f.ContentType).Returns("text/plain");
            fileMock.Setup(f => f.Length).Returns(stream.Length);
            fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Callback<Stream, CancellationToken>((s, c) =>
                {
                    // Reset stream position before copying to simulate a real file stream
                    stream.Position = 0;
                    stream.CopyTo(s);
                })
                .Returns(Task.CompletedTask);

            return (new List<IFormFile> { fileMock.Object }, fileContentBytes);
        }

        // Test Case 1: Happy Path - Successful Submission
        [Fact]
        [Trait("Category", "CreateAndSubmitPost - HappyPath")]
        public async Task CreateAndSubmitPost_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new CreateAndSubmitPostRequest(Guid.NewGuid(), "Title", "Content", new List<string> { "tag1" }, "Post");
            var (files, _) = MockFiles();
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Post created and submitted successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<CreateAndSubmitPostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateAndSubmitPost(request, files);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post created and submitted successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<CreateAndSubmitPostCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "CreateAndSubmitPost - AuthFailure")]
        public async Task CreateAndSubmitPost_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            SetAuthHeader(null); // No header
            var request = new CreateAndSubmitPostRequest(null, "Title", "Content", null, "Post");
            var (files, _) = MockFiles();

            // Act
            var result = await _controller.CreateAndSubmitPost(request, files);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<CreateAndSubmitPostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 3: Authentication Failure - Invalid Header Format
        [Fact]
        [Trait("Category", "CreateAndSubmitPost - AuthFailure")]
        public async Task CreateAndSubmitPost_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            SetAuthHeader("invalid-guid"); // Invalid format
            var request = new CreateAndSubmitPostRequest(null, "Title", "Content", null, "Post");
            var (files, _) = MockFiles();

            // Act
            var result = await _controller.CreateAndSubmitPost(request, files);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<CreateAndSubmitPostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 4: Failure Path - Handler Returns Bad Request (e.g., File upload failed)
        [Fact]
        [Trait("Category", "CreateAndSubmitPost - Failure")]
        public async Task CreateAndSubmitPost_WhenHandlerReturnsBadRequest_ReturnsBadRequestResponse()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new CreateAndSubmitPostRequest(null, "Title", "Content", null, "Post");
            var (files, _) = MockFiles();
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Failed to upload file", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<CreateAndSubmitPostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateAndSubmitPost(request, files);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Failed to upload file", result.Message);
        }

        // Test Case 5: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "CreateAndSubmitPost - Exception")]
        public async Task CreateAndSubmitPost_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new CreateAndSubmitPostRequest(null, "Title", "Content", null, "Post");
            var (files, _) = MockFiles();

            _senderMock.Setup(s => s.Send(It.IsAny<CreateAndSubmitPostCommand>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new TimeoutException("Operation timed out"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => _controller.CreateAndSubmitPost(request, files));
        }

        // Test Case 6: Mapping Check - Correct Data Passed To Command
        [Fact]
        [Trait("Category", "CreateAndSubmitPost - Mapping")]
        public async Task CreateAndSubmitPost_ShouldPassCorrectDataToCommand()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var tags = new List<string> { " dotnet ", " C# " }; // Test trimming/lowercase
            SetAuthHeader(userId.ToString());

            var request = new CreateAndSubmitPostRequest(categoryId, "Test Title", "Test Content", tags, "Solution");
            var (files, fileContentBytes) = MockFiles();

            CreateAndSubmitPostCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<CreateAndSubmitPostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as CreateAndSubmitPostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.CreateAndSubmitPost(request, files);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(userId, capturedCommand.AuthorId);
            Assert.Equal(categoryId, capturedCommand.CategoryId);
            Assert.Equal("Test Title", capturedCommand.Title);
            Assert.Equal("Test Content", capturedCommand.Content);
            Assert.Equal("Solution", capturedCommand.PostType);
            Assert.Equal(tags, capturedCommand.Tags);

            // Assert File Mapping
            Assert.NotNull(capturedCommand.FilesToUpload);
            Assert.Single(capturedCommand.FilesToUpload);
            var fileDto = capturedCommand.FilesToUpload[0];
            Assert.Equal("test.txt", fileDto.FileName);
            Assert.Equal("text/plain", fileDto.ContentType);
            Assert.Equal(fileContentBytes, fileDto.Content);
        }
    }
}
