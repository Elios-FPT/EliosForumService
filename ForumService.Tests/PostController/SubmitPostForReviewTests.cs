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
using static ForumService.Contract.UseCases.Post.Request;

namespace ForumService.Tests.PostController
{
    public class SubmitPostForReviewTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Post.PostController _controller;
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public SubmitPostForReviewTests()
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

        // Test Case 1: Happy Path - Successful Submission
        [Fact]
        [Trait("Category", "SubmitPost - HappyPath")]
        public async Task SubmitPostForReview_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new SubmitPostForReviewRequest(new List<string> { "tag1", "tag2" });
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Post submitted successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.Is<SubmitPostForReviewCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Post submitted successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<SubmitPostForReviewCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "SubmitPost - AuthFailure")]
        public async Task SubmitPostForReview_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader(null); // No header
            var request = new SubmitPostForReviewRequest(new List<string>());

            // Act
            var result = await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<SubmitPostForReviewCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 3: Authentication Failure - Invalid Header Format
        [Fact]
        [Trait("Category", "SubmitPost - AuthFailure")]
        public async Task SubmitPostForReview_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            var postId = Guid.NewGuid();
            SetAuthHeader("invalid-guid"); // Invalid format
            var request = new SubmitPostForReviewRequest(new List<string>());

            // Act
            var result = await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<SubmitPostForReviewCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 4: Failure Path - Post Not Found (Handler returns 404)
        [Fact]
        [Trait("Category", "SubmitPost - Failure")]
        public async Task SubmitPostForReview_WhenPostNotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new SubmitPostForReviewRequest(new List<string>());
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Post not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<SubmitPostForReviewCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Post not found.", result.Message);
            _senderMock.Verify(s => s.Send(It.Is<SubmitPostForReviewCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 5: Failure Path - User Not Authorized (Handler returns 403)
        [Fact]
        [Trait("Category", "SubmitPost - Failure")]
        public async Task SubmitPostForReview_WhenUserNotAuthorized_ReturnsForbiddenResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new SubmitPostForReviewRequest(new List<string>());
            var expectedResponse = new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to submit this post.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<SubmitPostForReviewCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("You are not authorized to submit this post.", result.Message);
        }

        // Test Case 6: Failure Path - Handler Returns Internal Server Error
        [Fact]
        [Trait("Category", "SubmitPost - Failure")]
        public async Task SubmitPostForReview_WhenHandlerReturnsInternalError_ReturnsInternalServerErrorResponse()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new SubmitPostForReviewRequest(new List<string>());
            var expectedResponse = new BaseResponseDto<bool> { Status = 500, Message = "Database update failed.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.Is<SubmitPostForReviewCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Database update failed.", result.Message);
        }

        // Test Case 7: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "SubmitPost - Exception")]
        public async Task SubmitPostForReview_WhenSenderThrowsException_ExceptionIsPropagated()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new SubmitPostForReviewRequest(new List<string>());

            _senderMock.Setup(s => s.Send(It.Is<SubmitPostForReviewCommand>(c => c.PostId == postId && c.RequesterId == userId), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new TimeoutException("Operation timed out"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => _controller.SubmitPostForReview(postId, request));
        }

        // Test Case 8: Mapping Check - Correct IDs and Tags Passed to Command
        [Fact]
        [Trait("Category", "SubmitPost - Mapping")]
        public async Task SubmitPostForReview_ShouldPassCorrectIdsAndTagsToCommand()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var tags = new List<string> { "tech", "dotnet", "csharp" };
            var request = new SubmitPostForReviewRequest(tags);
            SubmitPostForReviewCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<SubmitPostForReviewCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as SubmitPostForReviewCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(userId, capturedCommand.RequesterId);
            Assert.Equal(tags, capturedCommand.Tags);
        }

        // Test Case 9: Edge Case - Empty Tags List
        [Fact]
        [Trait("Category", "SubmitPost - EdgeCase")]
        public async Task SubmitPostForReview_WithEmptyTagList_ShouldPassEmptyListToCommand()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new SubmitPostForReviewRequest(new List<string>()); // Empty list
            SubmitPostForReviewCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<SubmitPostForReviewCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as SubmitPostForReviewCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.SubmitPostForReview(postId, request);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(postId, capturedCommand.PostId);
            Assert.Equal(userId, capturedCommand.RequesterId);
            Assert.NotNull(capturedCommand.Tags); // Should not be null
            Assert.Empty(capturedCommand.Tags); // Should be empty
        }

        // Test Case 10: Verification - Sender Call Count
        [Fact]
        [Trait("Category", "SubmitPost - Verification")]
        public async Task SubmitPostForReview_WhenAuthenticated_ShouldCallSenderSendExactlyOnce()
        {
            // Arrange
            var postId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var request = new SubmitPostForReviewRequest(new List<string>());
            _senderMock.Setup(s => s.Send(It.IsAny<SubmitPostForReviewCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.SubmitPostForReview(postId, request);

            // Assert
            _senderMock.Verify(s => s.Send(It.IsAny<SubmitPostForReviewCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
