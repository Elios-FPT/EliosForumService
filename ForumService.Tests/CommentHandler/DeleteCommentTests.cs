using ForumService.Contract.Shared;
using ForumService.Web.Controllers.Comment; 
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Comment.Command;

namespace ForumService.Tests.CommentController
{
    public class DeleteCommentTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Comment.CommentController _controller; 
        private readonly Mock<HttpContext> _httpContextMock;
        private readonly Mock<HttpRequest> _httpRequestMock;
        private readonly HeaderDictionary _headers;

        public DeleteCommentTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Comment.CommentController(_senderMock.Object);

            // Mock HttpContext and Headers
            _httpContextMock = new Mock<HttpContext>();
            _httpRequestMock = new Mock<HttpRequest>();
            _headers = new HeaderDictionary();
            _httpRequestMock.Setup(x => x.Headers).Returns(_headers);
            _httpContextMock.Setup(x => x.Request).Returns(_httpRequestMock.Object);

            // Cần mock Response.StatusCode
            var httpResponseMock = new Mock<HttpResponse>();
            httpResponseMock.SetupSet(r => r.StatusCode = It.IsAny<int>());
            _httpContextMock.Setup(x => x.Response).Returns(httpResponseMock.Object);

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

        // Test Case 1: Happy Path - Successful Delete
        [Fact]
        [Trait("Category", "DeleteComment - HappyPath")]
        public async Task DeleteComment_WhenSuccessful_ReturnsOkResponse()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Comment deleted successfully.", ResponseData = true };

            _senderMock.Setup(s => s.Send(It.Is<DeleteCommentCommand>(c =>
                c.CommentId == commentId &&
                c.RequesterId == userId
            ), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteComment(commentId);

            // Assert
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Comment deleted successfully.", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Test Case 2: Authentication Failure - Missing Header
        [Fact]
        [Trait("Category", "DeleteComment - AuthFailure")]
        public async Task DeleteComment_WhenAuthHeaderMissing_ReturnsUnauthorized()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            SetAuthHeader(null); // No header

            // Act
            var result = await _controller.DeleteComment(commentId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated or invalid/missing X-Auth-Request-User header", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 3: Authentication Failure - Invalid Header Format
        [Fact]
        [Trait("Category", "DeleteComment - AuthFailure")]
        public async Task DeleteComment_WhenAuthHeaderInvalidFormat_ReturnsUnauthorized()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            SetAuthHeader("invalid-guid"); // Invalid format

            // Act
            var result = await _controller.DeleteComment(commentId);

            // Assert
            Assert.Equal(401, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("User not authenticated or invalid/missing X-Auth-Request-User header", result.Message);
            _senderMock.Verify(s => s.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // Test Case 4: Failure Path - Comment Not Found (Handler returns 404)
        [Fact]
        [Trait("Category", "DeleteComment - Failure")]
        public async Task DeleteComment_WhenCommentNotFound_ReturnsNotFoundResponse()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Comment not found.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteComment(commentId);

            // Assert
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Comment not found.", result.Message);
        }

        // Test Case 5: Failure Path - User Not Authorized (Handler returns 403)
        [Fact]
        [Trait("Category", "DeleteComment - Failure")]
        public async Task DeleteComment_WhenUserNotAuthorized_ReturnsForbiddenResponse()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var expectedResponse = new BaseResponseDto<bool> { Status = 403, Message = "You are not authorized to delete this comment.", ResponseData = false };

            _senderMock.Setup(s => s.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteComment(commentId);

            // Assert
            Assert.Equal(403, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("You are not authorized to delete this comment.", result.Message);
        }

        // Test Case 6: Exception Path - Sender Throws Exception
        [Fact]
        [Trait("Category", "DeleteComment - Exception")]
        public async Task DeleteComment_WhenSenderThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            var exceptionMessage = "Database error";

            _senderMock.Setup(s => s.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()))
                       .ThrowsAsync(new Exception(exceptionMessage));

            // Act
            var result = await _controller.DeleteComment(commentId);

            // Assert
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.Contains(exceptionMessage, result.Message);
        }

        // Test Case 7: Mapping Check - Correct IDs Passed to Command
        [Fact]
        [Trait("Category", "DeleteComment - Mapping")]
        public async Task DeleteComment_ShouldPassCorrectIdsToCommand()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            SetAuthHeader(userId.ToString());
            DeleteCommentCommand? capturedCommand = null;

            _senderMock.Setup(s => s.Send(It.IsAny<DeleteCommentCommand>(), It.IsAny<CancellationToken>()))
                       .Callback<IRequest<BaseResponseDto<bool>>, CancellationToken>((cmd, token) => capturedCommand = cmd as DeleteCommentCommand)
                       .ReturnsAsync(new BaseResponseDto<bool> { Status = 200, ResponseData = true });

            // Act
            await _controller.DeleteComment(commentId);

            // Assert
            Assert.NotNull(capturedCommand);
            Assert.Equal(commentId, capturedCommand.CommentId);
            Assert.Equal(userId, capturedCommand.RequesterId);
        }
    }
}
