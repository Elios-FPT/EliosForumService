using ForumService.Contract.Shared;
using ForumService.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostController
{
    public class UpvotePostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public UpvotePostTests()
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

        // Test Case 1: Happy Path - Successful Upvote
        [Fact]
        [Trait("Category", "UpvotePost - HappyPath")]
        public async Task UpvotePost_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Post upvoted successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.Is<UpvotePostCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpvotePost(postId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post upvoted successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<UpvotePostCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "UpvotePost - AuthFailure")]
        public async Task UpvotePost_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(null); // No header

            // Act
            var result = await _controller.UpvotePost(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<UpvotePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 3: Authentication Failure - Invalid Header Format
        [Fact]
        [Trait("Category", "UpvotePost - AuthFailure")]
        public async Task UpvotePost_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader("invalid-guid"); // Invalid format

            // Act
            var result = await _controller.UpvotePost(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<UpvotePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 4: Failure Path - Post Not Found (Handler returns 404)
        [Fact]
        [Trait("Category", "UpvotePost - Failure")]
        public async Task UpvotePost_WhenPostNotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<UpvotePostCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpvotePost(postId);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Post not found.", result.Message);
        }

        // Test Case 5: Failure Path - User Not Authorized (Voting on own post, Handler returns 403)
        [Fact]
        [Trait("Category", "UpvotePost - Failure")]
        public async Task UpvotePost_WhenUserVotesOnOwnPost_ReturnsForbiddenResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<bool> { Status = 403, Message = "You cannot vote on your own post.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<UpvotePostCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpvotePost(postId);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("You cannot vote on your own post.", result.Message);
        }

        // Test Case 6: Failure Path - Handler Returns Internal Server Error
        [Fact]
        [Trait("Category", "UpvotePost - Failure")]
        public async Task UpvotePost_WhenHandlerReturnsInternalError_ReturnsInternalServerErrorResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<bool> { Status = 500, Message = "Database update failed.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<UpvotePostCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpvotePost(postId);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Database update failed.", result.Message);
        }

        // Test Case 7: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "UpvotePost - Exception")]
        public async Task UpvotePost_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());

            _senderMock.Setup(s => s.Send(It.Is<UpvotePostCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new TimeoutException("Operation timed out"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => _controller.UpvotePost(postId));
        }

        // Test Case 8: Mapping Check - Correct IDs Passed to Command
        [Fact]
        [Trait("Category", "UpvotePost - Mapping")]
        public async Task UpvotePost_ShouldPassCorrectIdsToCommand()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            UpvotePostCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<UpvotePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as UpvotePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.UpvotePost(postId);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(userId, capturedCommand.RequesterId);
        }

        // Test Case 9: Verification - Sender Call Count
        [Fact]
        [Trait("Category", "UpvotePost - Verification")]
        public async Task UpvotePost_WhenAuthenticated_ShouldCallSenderSendExactlyOnce()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            _senderMock.Setup(s => s.Send(It.IsAny<UpvotePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.UpvotePost(postId);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<UpvotePostCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
