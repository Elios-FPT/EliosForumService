using ForumService.Contract.Shared;
using ForumService.Web.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.PostController
{
    public class ApprovePostTests
    {
        private readonly Mock<ISender> _senderMock;
        // The Controller being tested (e.g., ModeratorPostController)
        private readonly Web.Controllers.Post.ModeratorPostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public ApprovePostTests()
        {
            _senderMock = new Mock<ISender>();
            // Ensure you use the correct Controller class name
            _controller = new Web.Controllers.Post.ModeratorPostController(_senderMock.Object);

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

        // Test Case 1: Happy Path - Successful Approval
        [Fact]
        [Trait("Category", "ApprovePost - HappyPath")]
        public async Task ApprovePost_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            SetAuthHeader(moderatorId.ToString());

            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Post approved and published successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.Is<ApprovePostCommand>(c => c.PostId == postId && c.ModeratorId == moderatorId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ApprovePost(postId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post approved and published successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<ApprovePostCommand>(c => c.PostId == postId && c.ModeratorId == moderatorId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "ApprovePost - AuthFailure")]
        public async Task ApprovePost_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(null); // No header

            // Act
            var result = await _controller.ApprovePost(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<ApprovePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 3: Authentication Failure - Invalid Header
        [Fact]
        [Trait("Category", "ApprovePost - AuthFailure")]
        public async Task ApprovePost_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader("invalid-guid"); // Invalid Guid format

            // Act
            var result = await _controller.ApprovePost(postId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<ApprovePostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 4: Business Logic Failure - Post Not Found (Handler returns 404)
        [Fact]
        [Trait("Category", "ApprovePost - Failure")]
        public async Task ApprovePost_WhenPostNotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            SetAuthHeader(moderatorId.ToString());

            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<ApprovePostCommand>(c => c.PostId == postId && c.ModeratorId == moderatorId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ApprovePost(postId);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Post not found.", result.Message);
        }

        // Test Case 5: Business Logic Failure - Invalid Post Status (Handler returns 400)
        [Fact]
        [Trait("Category", "ApprovePost - Failure")]
        public async Task ApprovePost_WhenPostStatusIsInvalid_ReturnsBadRequestResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            SetAuthHeader(moderatorId.ToString());

            var errorMessage = "Only posts with 'PendingReview' status can be approved. Current status is 'Published'.";
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = errorMessage, ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<ApprovePostCommand>(c => c.PostId == postId && c.ModeratorId == moderatorId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ApprovePost(postId);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal(errorMessage, result.Message);
        }

        // Test Case 6: System Error - Handler returns 500
        [Fact]
        [Trait("Category", "ApprovePost - Failure")]
        public async Task ApprovePost_WhenHandlerReturnsInternalError_ReturnsInternalErrorResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            SetAuthHeader(moderatorId.ToString());

            var errorMessage = "Failed to approve post: Database connection error";
            var expectedResponse = new BaseResponseDto<bool> { Status = 500, Message = errorMessage, ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<ApprovePostCommand>(c => c.PostId == postId && c.ModeratorId == moderatorId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.ApprovePost(postId);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal(errorMessage, result.Message);
        }

        // Test Case 7: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "ApprovePost - Exception")]
        public async Task ApprovePost_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            SetAuthHeader(moderatorId.ToString());

            _senderMock.Setup(s => s.Send(It.Is<ApprovePostCommand>(c => c.PostId == postId && c.ModeratorId == moderatorId), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new TimeoutException("Operation timed out"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => _controller.ApprovePost(postId));
        }

        // Test Case 8: Mapping Check - Ensures correct IDs are passed to Command
        [Fact]
        [Trait("Category", "ApprovePost - Mapping")]
        public async Task ApprovePost_ShouldPassCorrectIdsToCommand()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            SetAuthHeader(moderatorId.ToString());
            ApprovePostCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<ApprovePostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as ApprovePostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.ApprovePost(postId);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(moderatorId, capturedCommand.ModeratorId);
        }

        // Test Case 9: Verification - Sender Called Exactly Once
        [Fact]
        [Trait("Category", "ApprovePost - Verification")]
        public async Task ApprovePost_WhenAuthenticated_ShouldCallSenderSendExactlyOnce()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            SetAuthHeader(moderatorId.ToString());

            _senderMock.Setup(s => s.Send(It.IsAny<ApprovePostCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.ApprovePost(postId);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<ApprovePostCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}