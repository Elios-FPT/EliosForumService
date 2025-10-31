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
using static ForumService.Contract.UseCases.Post.Request;
using static ForumService.Contract.UseCases.Post.Command;

namespace ForumService.Tests.ModeratorPostController
{
    public class RejectPostTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.ModeratorPostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public RejectPostTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Post.ModeratorPostController(_senderMock.Object);

     
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
                _headers.Remove("X-Auth-Request-User");
            }
        }

        // Test Case 1: Happy Path - Successful Rejection
        [Fact]
        [Trait("Category", "RejectPost - HappyPath")]
        public async Task RejectPost_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var rejectionReason = "Post contains spam.";
            var request = new RejectPostRequest(rejectionReason);

            SetAuthHeader(moderatorId.ToString());

            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Post rejected successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(
                It.Is<RejectPostCommand>(c =>
                    c.PostId == postId &&
                    c.ModeratorId == moderatorId &&
                    c.Reason == rejectionReason),
                It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.RejectPost(postId, request);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post rejected successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<RejectPostCommand>(c => c.PostId == postId && c.ModeratorId == moderatorId && c.Reason == rejectionReason), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "RejectPost - AuthFailure")]
        public async Task RejectPost_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new RejectPostRequest("N/A");
            SetAuthHeader(null); // No header

            // Act
            var result = await _controller.RejectPost(postId, request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<RejectPostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 3: Authentication Failure - Invalid Header
        [Fact]
        [Trait("Category", "RejectPost - AuthFailure")]
        public async Task RejectPost_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var request = new RejectPostRequest("N/A");
            SetAuthHeader("invalid-guid"); // Invalid Guid format

            // Act
            var result = await _controller.RejectPost(postId, request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<RejectPostCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 4: Business Logic Failure - Post Not Found (Handler returns 404)
        [Fact]
        [Trait("Category", "RejectPost - Failure")]
        public async Task RejectPost_WhenPostNotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var request = new RejectPostRequest("Post not found");
            SetAuthHeader(moderatorId.ToString());

            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<RejectPostCommand>(c => c.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.RejectPost(postId, request);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Post not found.", result.Message);
        }

        // Test Case 5: Business Logic Failure - Invalid Post Status (Handler returns 400)
        [Fact]
        [Trait("Category", "RejectPost - Failure")]
        public async Task RejectPost_WhenPostStatusIsInvalid_ReturnsBadRequestResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var request = new RejectPostRequest("Bad status");
            SetAuthHeader(moderatorId.ToString());

            var errorMessage = "Only posts with 'PendingReview' status can be rejected. Current status is 'Published'.";
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = errorMessage, ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<RejectPostCommand>(c => c.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.RejectPost(postId, request);

            // Assert
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal(errorMessage, result.Message);
        }

        // Test Case 6: System Error - Handler returns 500
        [Fact]
        [Trait("Category", "RejectPost - Failure")]
        public async Task RejectPost_WhenHandlerReturnsInternalError_ReturnsInternalErrorResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var request = new RejectPostRequest("Error");
            SetAuthHeader(moderatorId.ToString());

            var errorMessage = "Failed to reject post: Database connection error";
            var expectedResponse = new BaseResponseDto<bool> { Status = 500, Message = errorMessage, ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<RejectPostCommand>(c => c.PostId == postId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.RejectPost(postId, request);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal(errorMessage, result.Message);
        }

        // Test Case 7: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "RejectPost - Exception")]
        public async Task RejectPost_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var request = new RejectPostRequest("Exception");
            SetAuthHeader(moderatorId.ToString());

            _senderMock.Setup(s => s.Send(It.Is<RejectPostCommand>(c => c.PostId == postId), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new TimeoutException("Operation timed out"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => _controller.RejectPost(postId, request));
        }

        // Test Case 8: Mapping Check - Ensures correct IDs and Reason are passed to Command
        [Fact]
        [Trait("Category", "RejectPost - Mapping")]
        public async Task RejectPost_ShouldPassCorrectIdsAndReasonToCommand()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var rejectionReason = "This is a specific test reason.";
            var request = new RejectPostRequest(rejectionReason);
            SetAuthHeader(moderatorId.ToString());

            RejectPostCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<RejectPostCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as RejectPostCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.RejectPost(postId, request);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(moderatorId, capturedCommand.ModeratorId);
            Assert.Equal(rejectionReason, capturedCommand.Reason);
        }
    }
}