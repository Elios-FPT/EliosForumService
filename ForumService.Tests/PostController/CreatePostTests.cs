using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Collections.Generic; 
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using static ForumService.Contract.UseCases.Post.Request;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostController
{
    public class CreatePostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly ForumService.Web.Controllers.Post.PostController _controller;
        private readonly Guid _testUserId = Guid.NewGuid();

        public CreatePostTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new ForumService.Web.Controllers.Post.PostController(_senderMock.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers["X-Auth-Request-User"] = _testUserId.ToString();
            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext,
            };
        }


        [Fact]
        [Trait("Category", "HappyPath")]
        public async Task CreatePost_WithValidData_ReturnsSuccess()
        {
            // Arrange
            // DTO Request mới không còn AttachmentIdsToLink
            var request = new CreatePostRequest(
                CategoryId: null,
                Title: "Valid Title",
                Content: "Valid Content",
                PostType: "Post"
            );

            var expectedResponse = new BaseResponseDto<bool> { Status = 200, ResponseData = true };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            // Verify Command mới không có Attachment
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c =>
                c.AuthorId == _testUserId &&
                c.Title == request.Title &&
                c.Content == request.Content
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        // --- BỎ CÁC TEST CASE VỀ ATTACHMENT ---
        // (Bỏ 'CreatePost_WithValidDataAndOneAttachment_ReturnsSuccess')
        // (Bỏ 'CreatePost_WithValidDataAndMultipleAttachments_ReturnsSuccess')


        [Fact]
        [Trait("Category", "Failure")]
        public async Task CreatePost_MissingAuthHeader_ReturnsUnauthorized()
        {
            // Arrange
            var httpContextWithoutHeader = new DefaultHttpContext();
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContextWithoutHeader };

            var request = new CreatePostRequest(null, "Title", "Content", "Post");

            // Act
            var result = await _controller.CreatePost(request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        [Trait("Category", "Failure")]
        public async Task CreatePost_InvalidAuthHeader_ReturnsUnauthorized()
        {
            // Arrange
            var httpContextInvalidHeader = new DefaultHttpContext();
            httpContextInvalidHeader.Request.Headers["X-Auth-Request-User"] = "not-a-guid";
            _controller.ControllerContext = new ControllerContext() { HttpContext = httpContextInvalidHeader };

            var request = new CreatePostRequest(null, "Title", "Content", "Post");

            // Act
            var result = await _controller.CreatePost(request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }


        [Fact]
        [Trait("Category", "Failure")]
        public async Task CreatePost_HandlerReturnsBadRequest_ControllerReturnsSameResponse()
        {
            // Arrange
            var request = new CreatePostRequest(
                CategoryId: null,
                Title: "", // Tiêu đề rỗng
                Content: "Content",
                PostType: "Post"
            );

            // Handler sẽ trả về lỗi 400
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "AuthorId, Title, and Content cannot be empty.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreatePost(request);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal(expectedResponse.Message, result.Message);
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c => c.AuthorId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "Exception")]
        public async Task CreatePost_SenderThrowsException_ShouldPropagateException()
        {
            // Arrange
            var request = new CreatePostRequest(null, "Exception Post", "Content", "Post");
            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new System.InvalidOperationException("Database connection failed"));

            // Act & Assert
            await Assert.ThrowsAsync<System.InvalidOperationException>(() => _controller.CreatePost(request));
            _senderMock.Verify(s => s.Send(It.Is<CreatePostCommand>(c => c.AuthorId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        [Trait("Category", "Mapping")]
        public async Task CreatePost_ShouldMapAllRequestDataToCommandCorrectly()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var request = new CreatePostRequest(
                CategoryId: categoryId,
                Title: "Mapping Test",
                Content: "Content text",
                PostType: "Solution"
            );

            CreatePostCommand capturedCommand = null;
            _senderMock.Setup(s => s.Send(It.IsAny<CreatePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as CreatePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.CreatePost(request);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(_testUserId, capturedCommand.AuthorId);
            Assert.Equal(request.CategoryId, capturedCommand.CategoryId);
            Assert.Equal(request.Title, capturedCommand.Title);
            Assert.Equal(request.Content, capturedCommand.Content);
            Assert.Equal(request.PostType, capturedCommand.PostType);
        }
    }
}