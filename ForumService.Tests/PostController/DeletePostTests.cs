using ForumService.Contract.Shared;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostController
{
    public class DeletePostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;  
        private readonly HeaderDictionary _headers;     

        private readonly Guid _testUserId = Guid.NewGuid();

        public DeletePostTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.PostController(_senderMock.Object);

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

        private void SetAuthHeader(string? value)
        {
            if (value != null)
            {
                _headers["X-Auth-Request-User"] = new StringValues(value);
            }
            else
            {
                // Xóa header nếu value là null
                _headers.Remove("X-Auth-Request-User");
            }
        }

        // Test Case 1: Happy Path - Successful Deletion
        [Fact]
        [Trait("Category", "DeletePost - HappyPath")]
        public async Task DeletePost_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(_testUserId.ToString()); 
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Post deleted successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post deleted successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Failure Path - Post Not Found
        [Fact]
        [Trait("Category", "DeletePost - Failure")]
        public async Task DeletePost_WhenPostNotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(_testUserId.ToString()); 
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(404, result.Status);
            // ... (rest of asserts)
            _senderMock.Verify(s => s.Send(It.Is<DeletePostCommand>(c => c.PostId == postId && c.RequesterId == _testUserId), It.IsAny<CancellationToken>()), Times.Once); // Verify sender was called
        }


        // Test Case 7: Verification - Sender Called Once
        [Fact]
        [Trait("Category", "DeletePost - Verification")]
        public async Task DeletePost_ShouldCallSenderSendExactlyOnce()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(_testUserId.ToString()); 
            _senderMock.Setup(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.DeletePost(postId);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()), Times.Once); 
        }

        [Fact]
        [Trait("Category", "DeletePost - AuthFailure")]
        public async Task DeletePost_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(null); // Không có header

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()), Times.Never); // Xác minh Send KHÔNG được gọi
        }

        [Fact]
        [Trait("Category", "DeletePost - AuthFailure")]
        public async Task DeletePost_WhenAuthHeaderInvalid_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader("not-a-valid-guid"); // Header không hợp lệ

            // Act
            var result = await _controller.DeletePost(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }


        [Fact]
        [Trait("Category", "DeletePost - Mapping")]
        public async Task DeletePost_ShouldPassCorrectPostIdAndUserIdFromHeaderToCommand()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(_testUserId.ToString()); 
            DeletePostCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<DeletePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as DeletePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.DeletePost(postId);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(_testUserId, capturedCommand.RequesterId); 
        }
    }
}

